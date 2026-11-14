using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace DeskDecor
{
    // ExplorerTAP 2026.2, API v4. COM and native calls stay on one MTA thread.
    internal sealed class XamlTaskbarEngine : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Queue<Action> _commands = new Queue<Action>();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly Thread _worker;
        private ITaskbarAppearanceService _service;
        private IntPtr _module;
        private bool _stopping;
        private int _generation;
        private readonly Action<bool, string> _stateChanged;

        public XamlTaskbarEngine(Action<bool, string> stateChanged)
        {
            _stateChanged = stateChanged;
            _worker = new Thread(Run) { IsBackground = true, Name = "DeskDecor XAML taskbar" };
            _worker.SetApartmentState(ApartmentState.MTA);
            _worker.Start();
        }

        public void Apply(bool enabled, List<IntPtr> taskbars)
        {
            int generation = Interlocked.Increment(ref _generation);
            Enqueue(delegate
            {
                try
                {
                    if (!enabled)
                    {
                        Restore();
                        _stateChanged(false, "已关闭");
                        return;
                    }
                    if (generation != _generation) return;
                    if (_service == null) Connect(taskbars[0]);
                    if (generation != _generation) return;
                    foreach (IntPtr taskbar in taskbars)
                    {
                        // SolidColor + ABGR alpha 0 removes the XAML BackgroundFill.
                        Check(_service.SetTaskbarAppearance(taskbar, 1, 0));
                        Check(_service.SetTaskbarBorderVisibility(taskbar, false));
                    }
                    _stateChanged(true, "XAML 清透已开启");
                }
                catch (Exception ex)
                {
                    // A lost Explorer connection must be rebuilt on a later request.
                    if (_service != null)
                    {
                        try { Restore(); } catch { }
                        try { Marshal.FinalReleaseComObject(_service); } catch { }
                        _service = null;
                    }
                    _stateChanged(false, "XAML 清透失败：" + ex.Message);
                    Log(ex.ToString());
                }
            });
        }

        private void Connect(IntPtr taskbar)
        {
            if (!Environment.Is64BitProcess)
                throw new InvalidOperationException("XAML 引擎需要 64 位程序");
            string dll = ExtractEngine();
            _module = LoadLibraryEx(dll, IntPtr.Zero, 0x00000100 | 0x00000800);
            if (_module == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "无法加载 XAML 引擎");
            IntPtr entry = GetProcAddress(_module, "InjectExplorerTAP");
            if (entry == IntPtr.Zero) throw new EntryPointNotFoundException("InjectExplorerTAP");
            InjectExplorerTap inject = (InjectExplorerTap)Marshal.GetDelegateForFunctionPointer(entry, typeof(InjectExplorerTap));
            Guid iid = typeof(ITaskbarAppearanceService).GUID;
            IntPtr unknown;
            Check(inject(taskbar, ref iid, out unknown));
            if (unknown == IntPtr.Zero) throw new InvalidOperationException("XAML 引擎没有返回服务");
            try { _service = (ITaskbarAppearanceService)Marshal.GetObjectForIUnknown(unknown); }
            finally { Marshal.Release(unknown); }
            Check(_service.RestoreAllTaskbarsToDefaultWhenProcessDies((uint)Process.GetCurrentProcess().Id));
            Log("Connected ExplorerTAP 2026.2 API v4");
        }

        private static string ExtractEngine()
        {
            const string expected = "C48F1A06168ACD7839269DDE0D15CCF5D09C702512B94DD6BA9A05DF1B153DC2";
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskDecor.runtime", "ExplorerTAP-2026.2-x64");
            Directory.CreateDirectory(folder);
            string dll = Path.Combine(folder, "ExplorerTAP.dll");
            foreach (string notice in new [] { "LICENSE.md", "THIRD-PARTY-NOTICES.md" })
            {
                string destination = Path.Combine(folder, notice);
                if (File.Exists(destination)) continue;
                using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream("DeskDecor." + notice))
                    if (source != null)
                        using (FileStream output = File.Create(destination)) source.CopyTo(output);
            }
            byte[] payload;
            using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream("DeskDecor.ExplorerTAP.dll"))
            {
                if (source == null) throw new FileNotFoundException("EXE 中缺少 XAML 引擎资源");
                using (MemoryStream target = new MemoryStream())
                {
                    source.CopyTo(target);
                    payload = target.ToArray();
                }
            }
            using (SHA256 hash = SHA256.Create())
            {
                if (BitConverter.ToString(hash.ComputeHash(payload)).Replace("-", "") != expected)
                    throw new InvalidDataException("XAML 引擎校验失败");
                bool valid = false;
                if (File.Exists(dll))
                    using (FileStream file = File.OpenRead(dll))
                        valid = BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "") == expected;
                if (!valid) File.WriteAllBytes(dll, payload);
            }
            return dll;
        }

        private void Enqueue(Action action)
        {
            lock (_gate) { if (_stopping) return; _commands.Enqueue(action); }
            _wake.Set();
        }

        private void Run()
        {
            int initialized = CoInitializeEx(IntPtr.Zero, 0);
            try
            {
                while (true)
                {
                    Action action = null;
                    lock (_gate)
                    {
                        if (_commands.Count > 0) action = _commands.Dequeue();
                        else if (_stopping) break;
                    }
                    if (action != null) action();
                    else _wake.WaitOne();
                }
                try { Restore(); } catch (Exception ex) { Log(ex.ToString()); }
                if (_service != null) { Marshal.FinalReleaseComObject(_service); _service = null; }
                // Keep the DLL loaded for the lifetime of the process: it owns the COM proxy/stub.
            }
            finally { if (initialized >= 0) CoUninitialize(); }
        }

        private void Restore()
        {
            if (_service != null) Check(_service.RestoreAllTaskbarsToDefault());
        }

        public void ResetConnection()
        {
            Enqueue(delegate
            {
                if (_service != null)
                {
                    try { Marshal.FinalReleaseComObject(_service); } catch { }
                    _service = null;
                }
            });
        }

        private static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
        private static void Log(string message)
        {
            try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskDecor.debug.log"),
                DateTime.Now.ToString("s") + " XAML " + message + Environment.NewLine); } catch { }
        }

        public void Dispose()
        {
            Interlocked.Increment(ref _generation);
            lock (_gate) _stopping = true;
            _wake.Set();
            _worker.Join(2000);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int InjectExplorerTap(IntPtr taskbar, ref Guid iid, out IntPtr service);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string filename, IntPtr reserved, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(IntPtr reserved, uint flags);
        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();

        [ComImport, Guid("5bcf9150-c28a-4ef2-913c-4c3ea2f5ead0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarAppearanceService
        {
            [PreserveSig] int SetTaskbarAppearance(IntPtr taskbar, int brush, uint color);
            [PreserveSig] int SetTaskbarBlur(IntPtr taskbar, uint color, float blurAmount);
            [PreserveSig] int ReturnTaskbarToDefaultAppearance(IntPtr taskbar);
            [PreserveSig] int SetTaskbarBorderVisibility(IntPtr taskbar, [MarshalAs(UnmanagedType.Bool)] bool visible);
            [PreserveSig] int RestoreAllTaskbarsToDefault();
            [PreserveSig] int RestoreAllTaskbarsToDefaultWhenProcessDies(uint pid);
            [PreserveSig] int KillExplorerWhenPackageUninstalls([MarshalAs(UnmanagedType.LPWStr)] string packageFullName);
        }
    }
}
