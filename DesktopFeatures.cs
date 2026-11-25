using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DeskDecor
{
    internal sealed class DesktopFeatures : IDisposable
    {
        private static IntPtr _lastOverlayParent;
        private static IntPtr _cachedDesktopParent;
        private string _lastTaskbarLogKey;
        private readonly Dictionary<IntPtr, Native.ACCENTPOLICY> _taskbarOriginals =
            new Dictionary<IntPtr, Native.ACCENTPOLICY>();
        private bool _iconsOriginalVisible;
        private bool _iconsStateCaptured;
        private bool _taskbarInitialized;
        private bool _taskbarRequested;
        private XamlTaskbarEngine _xaml;

        public bool TaskbarTransparent { get; private set; }
        public bool DesktopIconsHidden { get; private set; }
        public string TaskbarStatus { get; private set; }

        public void SetTaskbarTransparent(bool enabled)
        {
            if (_taskbarInitialized && _taskbarRequested == enabled && (!enabled || TaskbarTransparent)) return;
            _taskbarInitialized = true;
            _taskbarRequested = enabled;

            if (!enabled)
            {
                if (_xaml != null) _xaml.Apply(false, new List<IntPtr>());
                foreach (KeyValuePair<IntPtr, Native.ACCENTPOLICY> pair in _taskbarOriginals)
                    if (Native.IsWindow(pair.Key)) SetAccent(pair.Key, pair.Value);
                _taskbarOriginals.Clear();
                TaskbarTransparent = false;
                TaskbarStatus = "已关闭";
                LogTaskbarStatus(false, 0, 0, TaskbarStatus);
                return;
            }

            List<IntPtr> taskbars = FindTaskbars();
            if (taskbars.Count == 0)
            {
                TaskbarTransparent = false;
                TaskbarStatus = "未找到任务栏窗口";
                LogTaskbarStatus(enabled, 0, 0, TaskbarStatus);
                return;
            }

            foreach (IntPtr hwnd in taskbars)
            {
                if (HasXamlTaskbarContent(hwnd))
                {
                    TaskbarTransparent = true;
                    TaskbarStatus = "正在连接 XAML 清透引擎…";
                    if (_xaml == null) _xaml = new XamlTaskbarEngine(delegate(bool active, string status)
                    {
                        TaskbarTransparent = active;
                        TaskbarStatus = status;
                        LogTaskbarStatus(active, taskbars.Count, active ? taskbars.Count : 0, status);
                    });
                    _xaml.Apply(true, taskbars);
                    LogTaskbarStatus(true, taskbars.Count, 0, TaskbarStatus);
                    return;
                }
            }

            int applied = 0;
            int lastError = 0;
            foreach (IntPtr hwnd in taskbars)
            {
                Native.ACCENTPOLICY clear = new Native.ACCENTPOLICY
                {
                    AccentState = Native.ACCENT_ENABLE_TRANSPARENTGRADIENT,
                    AccentFlags = 0,
                    GradientColor = 0x00000000,
                    AnimationId = 0
                };
                if (SetAccentPreservingOriginal(hwnd, clear)) applied++;
                else lastError = Marshal.GetLastWin32Error();
            }
            TaskbarTransparent = applied == taskbars.Count;
            TaskbarStatus = applied == taskbars.Count
                ? "已发送清透请求，请以任务栏实际显示为准"
                : "系统未接受效果 (" + lastError + ")";
            LogTaskbarStatus(enabled, taskbars.Count, applied, TaskbarStatus);
        }

        private static bool HasXamlTaskbarContent(IntPtr taskbar)
        {
            bool found = false;
            Native.EnumChildWindows(taskbar, delegate(IntPtr child, IntPtr ignored)
            {
                string name = WindowClass(child);
                if (name == "Windows.UI.Composition.DesktopWindowContentBridge" ||
                    name == "Microsoft.UI.Content.DesktopChildSiteBridge")
                    found = true;
                return !found;
            }, IntPtr.Zero);
            return found;
        }

        private bool SetAccentPreservingOriginal(IntPtr hwnd, Native.ACCENTPOLICY policy)
        {
            if (!_taskbarOriginals.ContainsKey(hwnd)) _taskbarOriginals[hwnd] = ReadAccent(hwnd);
            return SetAccent(hwnd, policy);
        }

        private void LogTaskbarStatus(bool enabled, int count, int applied, string status)
        {
            string key = enabled + "|" + count + "|" + applied + "|" + status;
            if (key == _lastTaskbarLogKey) return;
            _lastTaskbarLogKey = key;
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskDecor.debug.log"),
                    DateTime.Now.ToString("s") + " taskbar transparent=" + enabled + " windows=" +
                    count + " applied=" + applied + " status=" + status + Environment.NewLine);
            }
            catch { }
        }

        public void SetDesktopIconsHidden(bool hidden)
        {
            bool wasHidden = DesktopIconsHidden;
            DesktopIconsHidden = hidden;
            IntPtr hwnd = FindDesktopIconWindow();
            if (hwnd == IntPtr.Zero)
            {
                if (!hidden) _iconsStateCaptured = false;
                return;
            }

            if (!_iconsStateCaptured)
            {
                _iconsOriginalVisible = Native.IsWindowVisible(hwnd);
                _iconsStateCaptured = true;
            }
            bool visible = !hidden && _iconsOriginalVisible;
            if (Native.IsWindowVisible(hwnd) != visible)
                Native.ShowWindow(hwnd, visible ? Native.SW_SHOW : Native.SW_HIDE);
            if (wasHidden && !hidden) _iconsStateCaptured = false;
        }

        public void Refresh()
        {
            if (DesktopIconsHidden) SetDesktopIconsHidden(true);
        }

        public void TaskbarRecreated()
        {
            _cachedDesktopParent = IntPtr.Zero;
            if (!_taskbarRequested) return;
            _taskbarInitialized = false;
            if (_xaml != null) _xaml.ResetConnection();
            SetTaskbarTransparent(true);
        }

        private static List<IntPtr> FindTaskbars()
        {
            List<IntPtr> result = new List<IntPtr>();
            AddTaskbarIfMatching(Native.FindWindow("Shell_TrayWnd", null), result);
            AddTaskbarIfMatching(Native.FindWindow("Shell_SecondaryTrayWnd", null), result);
            Native.EnumWindows(delegate(IntPtr hwnd, IntPtr ignored)
            {
                AddTaskbarIfMatching(hwnd, result);
                return true;
            }, IntPtr.Zero);
            try
            {
                foreach (Process process in Process.GetProcessesByName("explorer"))
                using (process)
                    foreach (ProcessThread thread in process.Threads)
                    using (thread)
                        Native.EnumThreadWindows((uint)thread.Id, delegate(IntPtr hwnd, IntPtr ignored)
                        {
                            AddTaskbarIfMatching(hwnd, result);
                            return true;
                        }, IntPtr.Zero);
            }
            catch { }
            return result;
        }

        private static void AddTaskbarIfMatching(IntPtr hwnd, List<IntPtr> result)
        {
            StringBuilder name = new StringBuilder(64);
            Native.GetClassName(hwnd, name, name.Capacity);
            if ((name.ToString() == "Shell_TrayWnd" || name.ToString() == "Shell_SecondaryTrayWnd") && !result.Contains(hwnd))
                result.Add(hwnd);
        }

        private static Native.ACCENTPOLICY ReadAccent(IntPtr hwnd)
        {
            Native.ACCENTPOLICY result = new Native.ACCENTPOLICY { AccentState = Native.ACCENT_DISABLED };
            IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.ACCENTPOLICY)));
            try
            {
                Native.WINDOWCOMPOSITIONATTRIBDATA data = new Native.WINDOWCOMPOSITIONATTRIBDATA
                {
                    Attribute = Native.WCA_ACCENT_POLICY,
                    Data = memory,
                    SizeOfData = Marshal.SizeOf(typeof(Native.ACCENTPOLICY))
                };
                if (Native.GetWindowCompositionAttribute(hwnd, ref data))
                    result = (Native.ACCENTPOLICY)Marshal.PtrToStructure(memory, typeof(Native.ACCENTPOLICY));
            }
            catch { }
            finally { Marshal.FreeHGlobal(memory); }
            return result;
        }

        private static bool SetAccent(IntPtr hwnd, Native.ACCENTPOLICY policy)
        {
            IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.ACCENTPOLICY)));
            try
            {
                Marshal.StructureToPtr(policy, memory, false);
                Native.WINDOWCOMPOSITIONATTRIBDATA data = new Native.WINDOWCOMPOSITIONATTRIBDATA
                {
                    Attribute = Native.WCA_ACCENT_POLICY,
                    Data = memory,
                    SizeOfData = Marshal.SizeOf(typeof(Native.ACCENTPOLICY))
                };
                return Native.SetWindowCompositionAttribute(hwnd, ref data);
            }
            catch { return false; }
            finally { Marshal.FreeHGlobal(memory); }
        }

        private static IntPtr FindDesktopIconWindow()
        {
            IntPtr parent = FindDesktopParent();
            IntPtr defView = parent == IntPtr.Zero ? IntPtr.Zero :
                Native.FindWindowEx(parent, IntPtr.Zero, "SHELLDLL_DefView", null);
            return defView == IntPtr.Zero ? IntPtr.Zero :
                Native.FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
        }

        public static IntPtr FindDesktopParent()
        {
            // Validate the actual icon host before reusing it: Explorer or a
            // wallpaper application may destroy, hide or replace this window.
            if (_cachedDesktopParent != IntPtr.Zero && Native.IsWindowVisible(_cachedDesktopParent) &&
                Native.FindWindowEx(_cachedDesktopParent, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                return _cachedDesktopParent;
            _cachedDesktopParent = IntPtr.Zero;
            IntPtr progman = Native.FindWindow("Progman", null);
            IntPtr iconHost = IntPtr.Zero;
            // The visible desktop icon host also owns WPE's wallpaper WorkerW
            // on current Windows 11. Other top-level WorkerW windows can be
            // hidden 170x47 notification windows and must never be used.
            Native.EnumWindows(delegate(IntPtr hwnd, IntPtr ignored)
            {
                if (!Native.IsWindowVisible(hwnd)) return true;
                IntPtr candidate = Native.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (candidate != IntPtr.Zero)
                {
                    iconHost = hwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            if (iconHost != IntPtr.Zero) return _cachedDesktopParent = iconHost;
            if (iconHost == IntPtr.Zero)
            {
                try
                {
                    foreach (Process process in Process.GetProcessesByName("explorer"))
                    using (process)
                    {
                        foreach (ProcessThread thread in process.Threads)
                        using (thread)
                        {
                            bool found = false;
                            Native.EnumThreadWindows((uint)thread.Id, delegate(IntPtr hwnd, IntPtr ignored)
                            {
                                if (!Native.IsWindowVisible(hwnd)) return true;
                                IntPtr candidate = Native.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                                if (candidate == IntPtr.Zero) return true;
                                iconHost = hwnd;
                                found = true;
                                return false;
                            }, IntPtr.Zero);
                            if (found) break;
                        }
                        if (iconHost != IntPtr.Zero) break;
                    }
                }
                catch { }
            }
            if (iconHost != IntPtr.Zero) return _cachedDesktopParent = iconHost;
            if (progman != IntPtr.Zero) return progman;
            IntPtr shell = Native.GetShellWindow();
            if (shell != IntPtr.Zero) return shell;
            return Native.GetDesktopWindow();
        }

        public static void AttachOverlayToDesktop(IntPtr overlay, Rectangle bounds)
        {
            if (overlay == IntPtr.Zero) return;
            IntPtr parent = FindDesktopParent();
            if (parent == IntPtr.Zero || parent == overlay)
            {
                LogDesktopAttachment(overlay, parent, bounds, "no-parent");
                return;
            }
            // Keep this a top-level layered popup. Layered cross-process children
            // can disappear behind Explorer's redirected desktop/WPE surfaces.
            // An owner relationship places it above the shell without TopMost.
            const int frameStyles = 0x00CF0000;
            const int edgeStyles = 0x00000301;
            int style = Native.GetWindowLongStyle(overlay, Native.GWL_STYLE);
            int exStyle = Native.GetWindowLongStyle(overlay, -20);
            bool child = (style & Native.WS_CHILD) != 0;
            bool frame = (style & frameStyles) != 0 || (exStyle & edgeStyles) != 0;
            bool topmost = (exStyle & 0x00000008) != 0;
            bool ownerChanged = Native.GetWindow(overlay, Native.GW_OWNER) != parent;
            if (child) Native.SetParent(overlay, IntPtr.Zero);
            if (child || frame || (style & Native.WS_POPUP) == 0)
                Native.SetWindowLongStyle(overlay, Native.GWL_STYLE,
                    (style & ~(Native.WS_CHILD | frameStyles)) | Native.WS_POPUP);
            if (frame) Native.SetWindowLongStyle(overlay, -20, exStyle & ~edgeStyles);
            if (child || ownerChanged)
                Native.SetWindowLongPointer(overlay, Native.GWLP_HWNDPARENT, parent);
            if (topmost)
                Native.SetWindowPos(overlay, new IntPtr(-2), 0, 0, 0, 0, // HWND_NOTOPMOST
                    Native.SWP_NOACTIVATE | Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOOWNERZORDER);

            // Activating an owned input window in older builds could raise the
            // whole desktop owner chain above apps. Repair that ordering as well
            // as the surface itself; anchoring to a raised Progman is insufficient.
            bool reachedDesktop = false, applicationBelowDesktop = false;
            Native.EnumWindows(delegate(IntPtr hwnd, IntPtr ignored)
            {
                if (hwnd == parent) { reachedDesktop = true; return true; }
                if (!reachedDesktop || hwnd == overlay || !Native.IsWindowVisible(hwnd)) return true;
                string name = WindowClass(hwnd);
                if (name == "WorkerW" || name == "Progman" || name == "Shell_TrayWnd" ||
                    name == "Shell_SecondaryTrayWnd") return true;
                // Only ordinary application windows are evidence of a raised shell.
                int extended = Native.GetWindowLongStyle(hwnd, -20);
                if ((extended & 0x00000080) == 0) applicationBelowDesktop = true;
                return !applicationBelowDesktop;
            }, IntPtr.Zero);
            if (applicationBelowDesktop)
                Native.SetWindowPos(parent, Native.HWND_BOTTOM, 0, 0, 0, 0,
                    Native.SWP_NOACTIVATE | Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOOWNERZORDER);

            // Insert immediately above the shell, below every ordinary app.
            IntPtr previous = Native.GetWindow(parent, Native.GW_HWNDPREV);
            Native.RECT current;
            bool correctBounds = Native.GetWindowRect(overlay, out current) &&
                current.left == bounds.Left && current.top == bounds.Top &&
                current.right == bounds.Right && current.bottom == bounds.Bottom;
            if (!child && !frame && !ownerChanged && previous == overlay &&
                correctBounds && Native.IsWindowVisible(overlay)) return;
            if (previous == overlay) previous = Native.GetWindow(overlay, Native.GW_HWNDPREV);
            Native.SetWindowPos(overlay, previous, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED | Native.SWP_SHOWWINDOW | Native.SWP_NOOWNERZORDER);
            if (_lastOverlayParent != parent)
                LogDesktopAttachment(overlay, parent, bounds, "desktop-owned-popup");
            _lastOverlayParent = parent;
        }
        private static void LogDesktopAttachment(IntPtr overlay, IntPtr parent, Rectangle bounds, string state)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskDecor.debug.log"),
                    DateTime.Now.ToString("s") + " " + state + " overlay=" + overlay + " parent=" + parent +
                    " bounds=" + bounds + " parentClass=" + WindowClass(parent) + Environment.NewLine);
            }
            catch { }
        }

        private static string WindowClass(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "<none>";
            StringBuilder name = new StringBuilder(128);
            Native.GetClassName(hwnd, name, name.Capacity);
            return name.ToString();
        }

        public static void RestoreDesktopIcons(bool wasVisible)
        {
            IntPtr hwnd = FindDesktopIconWindow();
            if (hwnd != IntPtr.Zero) Native.ShowWindow(hwnd, wasVisible ? Native.SW_SHOW : Native.SW_HIDE);
        }

        public void Dispose()
        {
            if (TaskbarTransparent) SetTaskbarTransparent(false);
            if (_xaml != null) _xaml.Dispose();
            if (DesktopIconsHidden)
            {
                IntPtr hwnd = FindDesktopIconWindow();
                if (hwnd != IntPtr.Zero)
                    Native.ShowWindow(hwnd, _iconsStateCaptured && !_iconsOriginalVisible ? Native.SW_HIDE : Native.SW_SHOW);
                DesktopIconsHidden = false;
            }
        }
    }
}
