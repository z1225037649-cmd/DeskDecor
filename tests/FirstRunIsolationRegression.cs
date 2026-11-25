using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security;
using System.Windows.Forms;
using DeskDecor;

internal static class FirstRunIsolationRegression
{
    internal sealed class StartupStore : IStartupStore
    {
        private string _value;
        public string Read() { return _value; }
        public void Write(string value) { _value = value; }
        public void Delete() { _value = null; }
    }

    private static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    private static string Mode;
    private static string OldSettings;
    private static string SourceHint;
    private static bool Verified;
    internal static string DesktopDirectory(bool shared)
    { return Path.Combine(Root, shared ? "public-desktop" : "user-desktop"); }

    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    [STAThread] private static void Main(string[] args)
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Mode = args[0];
            string current = Path.Combine(Root, "DeskDecor.settings.xml");
            string old = Path.Combine(Root, "older-copy");
            Directory.CreateDirectory(old);
            if (Mode == "fresh")
            {
                Directory.CreateDirectory(DesktopDirectory(false));
                Directory.CreateDirectory(DesktopDirectory(true));
                string folder = Path.Combine(DesktopDirectory(false), "folder");
                Directory.CreateDirectory(Path.Combine(folder, "nested"));
                File.WriteAllText(Path.Combine(folder, "nested", "private.txt"), "keep nested content");
                File.WriteAllText(Path.Combine(DesktopDirectory(false), "file.txt"), "desktop file");
                File.WriteAllText(Path.Combine(DesktopDirectory(true), "public.txt"), "public file");
                File.WriteAllText(Path.Combine(DesktopDirectory(false), "desktop.ini"), "metadata");
                File.WriteAllText(Path.Combine(DesktopDirectory(true), "Thumbs.db"), "metadata");
                var previous = new AppSettings { DesktopInitialized = true };
                previous.Group.Title = "Must not import the older copy";
                previous.Items.Add(new DesktopItem(Path.Combine(old, "personal.txt"), 0, 0));
                SettingsStore.Save(Path.Combine(old, "DeskDecor.settings.xml"), previous);
                File.WriteAllText(Path.Combine(old, "personal.txt"), "must not migrate or delete");
                Directory.CreateDirectory(Path.Combine(Root, ".migration"));
                File.WriteAllText(Path.Combine(Root, ".migration", "migration-source.xml"),
                    "<LocalInstallation><Directory>" + SecurityElement.Escape(old) + "</Directory></LocalInstallation>");
                File.WriteAllText(Path.Combine(Root, ".migration", "pending-migration.xml"), "obsolete record must be ignored");
            }
            else
            {
                AppSettings saved = SettingsStore.Load(current);
                if (Mode == "saved")
                {
                    saved.Group.Title = "My saved group";
                    saved.Items.RemoveAll(item => item.Path.EndsWith("file.txt", StringComparison.OrdinalIgnoreCase));
                    SettingsStore.Save(current, saved);
                    File.WriteAllText(Path.Combine(DesktopDirectory(false), "new-after-first-run.txt"), "do not import again");
                }
                else if (Mode == "empty")
                {
                    saved.Items.Clear();
                    SettingsStore.Save(current, saved);
                }
                else if (Mode == "backup")
                {
                    File.Copy(current, current + ".bak", true);
                    File.Delete(current);
                }
                else throw new ArgumentException("Unknown fixture mode");
            }
            OldSettings = File.ReadAllText(Path.Combine(old, "DeskDecor.settings.xml"));
            SourceHint = File.ReadAllText(Path.Combine(Root, ".migration", "migration-source.xml"));
            Directory.SetCurrentDirectory(old);
            typeof(DeskDecor.Program).GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Expect(Verified, "Startup must reach and validate the actual application context");
            Console.WriteLine("PASS: real startup " + Mode + " uses only adjacent settings and immediate desktop entries; old files untouched");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + (ex.InnerException ?? ex));
            Environment.ExitCode = 1;
        }
    }

    internal static void Verify(DeskDecor.AppContext context, string dataPath)
    {
        try
        {
            Expect(dataPath == Path.Combine(Root, "DeskDecor.settings.xml"), "Working directory must not redirect settings");
            AppSettings actual = SettingsStore.Load(dataPath);
            Expect(actual.DesktopInitialized, "First import must be persisted");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DesktopItem item in actual.Items) paths.Add(item.Path);
            if (Mode == "fresh")
            {
                Expect(paths.SetEquals(new[] {
                    Path.Combine(DesktopDirectory(false), "folder"),
                    Path.Combine(DesktopDirectory(false), "file.txt"),
                    Path.Combine(DesktopDirectory(true), "public.txt") }),
                    "Fresh startup must import both desktops once, without nested files, metadata or old entries");
            }
            else
            {
                Expect(actual.Group.Title == "My saved group", "Restart must preserve the local group title");
                Expect(actual.Items.Count == (Mode == "saved" ? 2 : 0), "Restart must preserve removals and deliberately empty groups");
                Expect(!paths.Contains(Path.Combine(DesktopDirectory(false), "new-after-first-run.txt")), "Restart must not rescan desktops");
            }
            string old = Path.Combine(Root, "older-copy");
            Expect(File.ReadAllText(Path.Combine(old, "DeskDecor.settings.xml")) == OldSettings, "Old settings must stay untouched");
            Expect(File.Exists(Path.Combine(old, "DeskDecor.exe")) &&
                File.ReadAllText(Path.Combine(old, "personal.txt")) == "must not migrate or delete", "Old executable and personal files must remain intact");
            Expect(File.ReadAllText(Path.Combine(Root, ".migration", "migration-source.xml")) == SourceHint &&
                File.ReadAllText(Path.Combine(Root, ".migration", "pending-migration.xml")) == "obsolete record must be ignored",
                "Leftover migration records must not be resumed or rewritten");
            Expect(!Directory.Exists(Path.Combine(Root, "迁移文件")), "Startup must not create migrated content");
            var overlay = (OverlayForm)typeof(DeskDecor.AppContext).GetField("_overlay", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context);
            Expect(!overlay.Visible && !overlay.IsHandleCreated, "Fixture must not present or attach a desktop surface");
            Verified = true;
        }
        finally { context.ExitThread(); }
    }
}
