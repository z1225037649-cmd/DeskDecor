using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DeskDecor;

internal static class SettingsVisibilityRegression
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr mode,
        uint flags, uint access, IntPtr security);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDesktop(uint thread);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetUserObjectInformation(IntPtr handle, int index,
        StringBuilder value, uint length, out uint needed);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size; public string Reserved, Desktop, Title;
        public uint X, Y, Width, Height, CharsX, CharsY, Fill, Flags;
        public ushort Show, ReservedSize;
        public IntPtr ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo
    { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder command,
        IntPtr processSecurity, IntPtr threadSecurity, bool inheritHandles, uint flags,
        IntPtr environment, string directory, ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll")]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint code);
    [DllImport("kernel32.dll")]
    private static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    internal sealed class StartupStore : IStartupStore
    {
        private string _value;
        public string Read() { return _value; }
        public void Write(string value) { _value = value; }
        public void Delete() { _value = null; }
    }

    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private static T Field<T>(object value, string name)
    {
        return (T)value.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    }

    private static SettingsForm OpenSettings(DeskDecor.AppContext context)
    {
        typeof(DeskDecor.AppContext).GetMethod("ShowSettings", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(context, new object[] { false });
        Application.DoEvents();
        SettingsForm form = Field<SettingsForm>(context, "_settingsForm");
        Expect(form.Visible && Native.IsWindowVisible(form.Handle), "The real settings window must be visible");
        return form;
    }

    private static void ExpectOverlay(OverlayForm overlay, bool visible, string message)
    {
        Expect(overlay.Visible == visible && Native.IsWindowVisible(overlay.Handle) == visible, message);
    }

    private static void Click(SettingsForm form, string text)
    {
        foreach (Control control in form.Controls)
        {
            Button button = control as Button;
            if (button != null && button.Text == text)
            {
                button.PerformClick();
                Application.DoEvents();
                return;
            }
        }
        throw new Exception("Missing real settings button: " + text);
    }

    // Read the bitmap already rendered by the production event chain; do not
    // call PaintSurface/Redraw here, which could conceal a missing live update.
    private static string RenderedDigest(OverlayForm overlay)
    {
        LayeredSurface surface = Field<LayeredSurface>(overlay, "_surface");
        Bitmap bitmap = Field<Bitmap>(surface, "_surface");
        Expect(bitmap != null && surface.LastError == 0, "The actual layered surface must render and push successfully");
        using (var bytes = new MemoryStream())
        using (var hash = SHA256.Create())
        {
            bitmap.Save(bytes, ImageFormat.Png);
            return Convert.ToBase64String(hash.ComputeHash(bytes.ToArray()));
        }
    }

    private static void Run()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles();
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixture.settings.xml");
        string itemPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preview.txt");
        File.WriteAllText(itemPath, "isolated settings preview fixture");
        var settings = new AppSettings { DesktopInitialized = true, IconSize = 16, Opacity = 100 };
        settings.Group.X = 20; settings.Group.Y = 20;
        settings.Group.Width = 320; settings.Group.Height = 240;
        settings.Groups.Add(settings.Group);
        settings.ActiveGroupId = settings.Group.Id;
        settings.Items.Add(new DesktopItem(itemPath, 0, 0) { GroupId = settings.Group.Id });
        SettingsStore.Save(path, settings);
        DeskDecor.AppContext context = null;
        using (var show = new EventWaitHandle(false, EventResetMode.AutoReset))
        {
            try
            {
                context = new DeskDecor.AppContext(path, show);
                OverlayForm overlay = Field<OverlayForm>(context, "_overlay");
                ExpectOverlay(overlay, true, "The desktop surface must start visible");
                SettingsForm form = OpenSettings(context);
                ExpectOverlay(overlay, true, "Opening settings must keep the desktop surface visible");
                Expect(ReferenceEquals(form, OpenSettings(context)), "Repeated opening must reuse the settings window");
                ExpectOverlay(overlay, true, "Repeated opening must keep the desktop surface visible");

                string before = RenderedDigest(overlay);
                Field<NumericUpDown>(form, "_iconSize").Value = 32;
                Expect(overlay.Settings.IconSize == 32 && SettingsStore.Load(path).IconSize == 32,
                    "Changing the real icon-size control must immediately update and save settings");
                string resized = RenderedDigest(overlay);
                Expect(before != resized, "Changing icon size must immediately repaint the existing surface");
                Field<NumericUpDown>(form, "_opacity").Value = 35;
                Expect(overlay.Settings.Opacity == 35 && SettingsStore.Load(path).Opacity == 35,
                    "Changing the real opacity control must immediately update and save settings");
                Expect(resized != RenderedDigest(overlay), "Changing opacity must immediately repaint the existing surface");
                ExpectOverlay(overlay, true, "Appearance edits must keep the surface visible");

                Click(form, "完成");
                Expect(!form.Visible, "Done must hide settings");
                ExpectOverlay(overlay, true, "Done must retain the visible surface");
                form = OpenSettings(context);
                ExpectOverlay(overlay, true, "Reopening after Done must retain the visible surface");
                form.Close();
                Application.DoEvents();
                Expect(form.IsDisposed, "Closing settings must dispose that window");
                ExpectOverlay(overlay, true, "Closing settings must retain the visible surface");
                form = OpenSettings(context);
                ExpectOverlay(overlay, true, "Recreating closed settings must retain the visible surface");

                Click(form, "隐藏组件");
                ExpectOverlay(overlay, false, "The explicit hide toggle must hide the surface");
                OpenSettings(context);
                ExpectOverlay(overlay, false, "Repeated opening must respect explicit hiding");
                Click(form, "完成");
                ExpectOverlay(overlay, false, "Done must respect explicit hiding");
                form = OpenSettings(context);
                ExpectOverlay(overlay, false, "Reopening after Done must respect explicit hiding");
                form.Close();
                Application.DoEvents();
                ExpectOverlay(overlay, false, "Closing settings must respect explicit hiding");
                form = OpenSettings(context);
                ExpectOverlay(overlay, false, "Recreating closed settings must respect explicit hiding");
                Click(form, "显示组件");
                ExpectOverlay(overlay, true, "The explicit show toggle must restore the surface while settings stays open");
                Expect(form.Visible, "Showing the surface must leave settings open");
                Console.WriteLine("PASS: real settings open/reopen/Done/Close preserve visible and explicitly hidden surfaces");
                Console.WriteLine("PASS: icon size and opacity immediately repaint the layered bitmap and persist through the real event chain");
            }
            finally { if (context != null) context.ExitThread(); }
        }
    }

    [STAThread] private static int Main(string[] args)
    {
        // No SwitchDesktop call: all real test windows stay on an unshown,
        // private desktop. Refuse to fall back to the user's input desktop.
        string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "visibility-result.txt");
        if (args.Length == 2 && args[0] == "--isolated")
        {
            using (var writer = new StreamWriter(report, false, Encoding.UTF8))
            {
                writer.AutoFlush = true;
                Console.SetOut(writer); Console.SetError(writer);
                try
                {
                    uint needed;
                    var name = new StringBuilder(256);
                    Expect(args[1].StartsWith("DeskDecorSettingsTest-", StringComparison.Ordinal) &&
                        GetUserObjectInformation(GetThreadDesktop(GetCurrentThreadId()), 2, name, 512, out needed) &&
                        name.ToString() == args[1], "The test process must run on its private desktop");
                    Run();
                    Console.WriteLine("PASS: all test windows stayed on a private desktop; user desktop and configuration were not modified");
                    return 0;
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine("FAIL: " + (error.InnerException ?? error).Message);
                    return 1;
                }
            }
        }
        string desktopName = "DeskDecorSettingsTest-" + Guid.NewGuid().ToString("N");
        IntPtr desktop = CreateDesktop(desktopName,
            IntPtr.Zero, IntPtr.Zero, 0, 0x01FF, IntPtr.Zero);
        if (desktop == IntPtr.Zero)
        {
            Console.Error.WriteLine("FAIL: cannot create isolated test desktop: " + Marshal.GetLastWin32Error());
            return 1;
        }
        ProcessInfo process = new ProcessInfo();
        try
        {
            // Bind before CLR/STA initialization can create hidden windows.
            var startup = new StartupInfo { Size = Marshal.SizeOf(typeof(StartupInfo)), Desktop = desktopName };
            string executable = Assembly.GetExecutingAssembly().Location;
            Expect(CreateProcess(executable, new StringBuilder("\"" + executable + "\" --isolated " + desktopName),
                IntPtr.Zero, IntPtr.Zero, false, 0x08000000, IntPtr.Zero,
                AppDomain.CurrentDomain.BaseDirectory, ref startup, out process),
                "Cannot start isolated test process: " + Marshal.GetLastWin32Error());
            if (WaitForSingleObject(process.Process, 45000) != 0)
            {
                TerminateProcess(process.Process, 1);
                WaitForSingleObject(process.Process, 5000);
                throw new Exception("Isolated settings regression timed out");
            }
            if (File.Exists(report)) Console.Write(File.ReadAllText(report));
            uint code;
            Expect(GetExitCodeProcess(process.Process, out code), "Cannot read isolated test result");
            return (int)code;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error.Message);
            return 1;
        }
        finally
        {
            if (process.Thread != IntPtr.Zero) CloseHandle(process.Thread);
            if (process.Process != IntPtr.Zero) CloseHandle(process.Process);
            CloseDesktop(desktop);
        }
    }
}
