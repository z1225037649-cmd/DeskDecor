using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using DeskDecor;

internal static class AppContextPersistenceRegression
{
    internal sealed class StartupStore : IStartupStore
    {
        private string _value;
        public string Read() { return _value; }
        public void Write(string value) { _value = value; }
        public void Delete() { _value = null; }
    }

    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    [STAThread] private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixture.settings.xml");
        DeskDecor.AppContext context = null;
        using (var show = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset))
        {
            try
            {
                var settings = new AppSettings { DesktopInitialized = true };
                settings.Group.Title = "Source";
                settings.Group.X = 20; settings.Group.Y = 20;
                settings.Group.Width = 320; settings.Group.Height = 300;
                var target = new GroupLayout { Title = "Target", X = 500, Y = 20, Width = 320, Height = 300 };
                settings.Groups.Add(settings.Group); settings.Groups.Add(target);
                settings.ActiveGroupId = settings.Group.Id;
                settings.Items.Add(new DesktopItem(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "one.txt"), 0, 0) { GroupId = settings.Group.Id });
                settings.Items.Add(new DesktopItem(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "two.txt"), 0, 0) { GroupId = target.Id });
                SettingsStore.Save(path, settings);
                context = new DeskDecor.AppContext(path, show);
                typeof(DeskDecor.AppContext).GetMethod("ShowSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(context, new object[] { false });
                var form = (SettingsForm)typeof(DeskDecor.AppContext).GetField("_settingsForm", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context);
                var overlay = (OverlayForm)typeof(DeskDecor.AppContext).GetField("_overlay", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context);
                Button delete = null;
                foreach (Control control in form.Controls)
                    if (control is Button && control.Text == "删除分组") delete = (Button)control;
                Expect(delete != null, "The real settings delete button must exist");
                byte[] before = File.ReadAllBytes(path);
                typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(delete, new object[] { EventArgs.Empty });
                AppSettings current = SettingsStore.Load(path);
                Expect(current.Groups.Count == 1 && current.Groups[0].Id == target.Id && current.Items.Count == 2 &&
                    current.Items.TrueForAll(item => item.GroupId == target.Id), "The actual AppContext event chain must migrate and save every entry");
                Expect(File.Exists(path + ".bak") && Convert.ToBase64String(before) == Convert.ToBase64String(File.ReadAllBytes(path + ".bak")),
                    "One group deletion must retain the exact pre-deletion primary as its rolling backup");
                Expect(!form.Visible && !overlay.Visible && !overlay.IsHandleCreated,
                    "Application event-chain checks must not show or attach test windows");
                Console.WriteLine("PASS: actual AppContext deletion event chain saves once and retains the exact pre-deletion backup");
                bool exited = false;
                context.ThreadExit += delegate { exited = true; };
                overlay.Settings.Group.Title = "saved before upgrade handoff";
                context.StartUpgradeHandoff();
                using (var handoff = System.Threading.EventWaitHandle.OpenExisting(LocalUpgrade.PrepareEventName)) handoff.Set();
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                while (!exited && deadline.ElapsedMilliseconds < 1500)
                { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                if (exited) context = null;
                Expect(exited && SettingsStore.Load(path).Group.Title == "saved before upgrade handoff",
                    "An upgrade request must persist current settings and exit through the real message-window event chain");
                Console.WriteLine("PASS: isolated upgrade handoff saves current configuration and closes normally through the real application event chain");
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAIL: " + (error.InnerException ?? error).Message);
                Environment.ExitCode = 1;
            }
            finally { if (context != null) context.ExitThread(); }
        }
    }
}
