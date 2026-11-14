using System;
using System.IO;
using System.Xml.Serialization;
using DeskDecor;

internal static class EntryFeaturesRegression
{
    private sealed class StartupStore : IStartupStore
    {
        public string Value;
        public int Writes, Deletes;
        public string Read() { return Value; }
        public void Write(string command) { Value = command; Writes++; }
        public void Delete() { Value = null; Deletes++; }
    }
    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    public static void Main()
    {
        var store = new StartupStore();
        bool originalExists = true;
        var startup = new StartupRegistration(@"C:\Portable apps\DeskDecor.exe", store, path => originalExists);
        Expect(!startup.Enabled && store.Writes == 0, "Startup must default off without writing anything");
        startup.SetEnabled(true);
        Expect(startup.Enabled && store.Value == "\"C:\\Portable apps\\DeskDecor.exe\"", "Run command must quote the exact portable EXE path");
        Expect(!startup.DisableIfLocationChanged() && store.Deletes == 0, "Unchanged location must keep startup enabled");
        var moved = new StartupRegistration(@"D:\Moved\DeskDecor.exe", store, path => true);
        Expect(!moved.Enabled, "Another EXE path must not be reported as this copy's autostart");
        Expect(moved.DisableIfLocationChanged() && store.Value == null, "Launching from a new location must remove the stale registration");
        Expect(!moved.DisableIfLocationChanged() && store.Deletes == 1, "Repeated checks must not delete or notify again");
        moved.SetEnabled(true);
        Expect(moved.Enabled && !startup.Enabled, "Re-enabling after a move must replace the previous path");
        moved.SetEnabled(false);
        Expect(!moved.Enabled && store.Deletes == 2, "Disabling must remove the startup entry");
        startup.SetEnabled(true);
        originalExists = false;
        Expect(startup.DisableIfLocationChanged() && !startup.Enabled, "Moving the executable while running must clear its startup entry");
        Expect(!startup.DisableIfLocationChanged() && store.Deletes == 3, "A cleared entry must stay off without repeated changes");
        bool blocked = false;
        try { startup.SetEnabled(true); } catch (IOException) { blocked = true; }
        Expect(blocked && store.Value == null, "A running copy at the old location must not register a missing EXE");
        Console.WriteLine("PASS: startup path changes clear the old entry once, retain unchanged entries and reject missing EXEs (fake registry only)");

        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "entry-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "sample.txt");
        string folder = Path.Combine(root, "folder");
        string metadata = Path.Combine(root, "desktop.ini");
        try
        {
            File.WriteAllText(file, "preserve this original");
            File.WriteAllText(metadata, "metadata");
            Directory.CreateDirectory(folder);
            var settings = new AppSettings { DesktopInitialized = true };
            int added = DesktopEntries.AddPaths(settings, new[] { file, folder, file.ToUpperInvariant(),
                folder + Path.DirectorySeparatorChar, metadata, Path.Combine(root, "missing.txt"), "", null });
            Expect(added == 2 && settings.Items.Count == 2, "Multi-file import must normalize, deduplicate, reject missing paths and hide metadata");
            Expect(settings.Items[0].Path == file && settings.Items[1].Path == folder, "Import must preserve input order");
            Expect(DesktopEntries.Remove(settings, settings.Items[0]), "Context removal must remove the entry");
            Expect(File.ReadAllText(file) == "preserve this original" && Directory.Exists(folder), "Removing a component icon must never alter its target");
            settings.Group = new GroupLayout { Title = "常用工具", X = 80, Y = 120, Width = 320, Height = 470, Collapsed = true };
            DesktopEntries.Remove(settings, settings.Items[0]);
            var serializer = new XmlSerializer(typeof(AppSettings));
            using (var text = new StringWriter())
            {
                serializer.Serialize(text, settings);
                using (var reader = new StringReader(text.ToString()))
                {
                    var restored = (AppSettings)serializer.Deserialize(reader);
                    Expect(restored.Group.Collapsed && restored.Group.Height == 470 && restored.Group.X == 80,
                        "Collapse must persist without overwriting expanded geometry");
                    Expect(restored.Group.DisplayTitle == "常用工具", "Custom group names must persist across restart");
                    restored.Group.Title = "  ";
                    Expect(restored.Group.DisplayTitle == "快捷入口", "An empty name must use the default title");
                    Expect(restored.DesktopInitialized && restored.Items.Count == 0,
                        "Removing every icon must persist an initialized empty list");
                }
            }
            Console.WriteLine("PASS: batch import, duplicates, non-destructive removal, collapsed geometry and empty-list persistence");
        }
        finally { Directory.Delete(root, true); }
    }
}
