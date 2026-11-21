using System;
using System.IO;
using DeskDecor;

internal static class SettingsStoreRegression
{
    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private static AppSettings WithTitle(string title)
    {
        return new AppSettings { Group = new GroupLayout { Title = title, X = 31, Y = 47, Width = 320, Height = 240 } };
    }

    public static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "DeskDecor-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "DeskDecor.settings.xml");
        try
        {
            var legacy = new AppSettings { Group = new GroupLayout { Title = "旧版" } };
            legacy.Items.Add(new DesktopItem("C:\\legacy.txt", 10, 20));
            legacy.Normalize();
            Expect(legacy.Groups.Count == 1 && legacy.Items[0].GroupId == legacy.Group.Id &&
                legacy.ActiveGroupId == legacy.Group.Id,
                "Legacy single-group settings must migrate entries and the active group without loss");

            GroupLayout extraGroup = new GroupLayout { Title = "文档" };
            legacy.Groups.Add(extraGroup);
            legacy.Items[0].GroupId = extraGroup.Id;
            legacy.ActiveGroupId = extraGroup.Id;
            legacy.Normalize();
            Expect(legacy.Groups.Count == 2 && legacy.Items[0].GroupId == extraGroup.Id && legacy.ActiveGroupId == extraGroup.Id,
                "Multiple groups and their entry assignments must survive normalization");

            SettingsStore.Save(path, WithTitle("first"));
            SettingsStore.Save(path, WithTitle("second"));
            string warning;
            bool recovered;
            AppSettings loaded = SettingsStore.Load(path, out warning, out recovered);
            Expect(loaded.Group.Title == "second" && !recovered && warning == null,
                "A valid primary configuration must load without a warning");
            loaded = SettingsStore.Load(path + ".bak", out warning, out recovered);
            Expect(loaded.Group.Title == "first", "A successful replacement must retain the previous configuration as backup");

            File.Delete(path);
            loaded = SettingsStore.Load(path, out warning, out recovered);
            Expect(recovered && loaded.Group.Title == "first" && File.Exists(path) && !string.IsNullOrEmpty(warning),
                "A missing primary configuration must recover from and preserve its valid backup");

            File.WriteAllText(path, "<broken>");
            loaded = SettingsStore.Load(path, out warning, out recovered);
            Expect(recovered && loaded.Group.Title == "first" && !string.IsNullOrEmpty(warning),
                "A corrupt primary must recover the last valid backup and report it");
            loaded = SettingsStore.Load(path, out warning, out recovered);
            Expect(!recovered && loaded.Group.Title == "first", "Backup recovery must repair the primary configuration");

            string backupBefore = File.ReadAllText(path + ".bak");
            SettingsStore.Save(path, WithTitle("recovered change"), true);
            Expect(File.ReadAllText(path + ".bak") == backupBefore,
                "The known-good recovery backup must survive the first save after recovery");
            SettingsStore.Save(path, WithTitle("latest"));
            loaded = SettingsStore.Load(path + ".bak", out warning, out recovered);
            Expect(loaded.Group.Title == "recovered change",
                "Subsequent successful saves must advance the backup to the previous primary");

            byte[] original = File.ReadAllBytes(path);
            using (FileStream locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bool failed = false;
                try { SettingsStore.Save(path, WithTitle("must not replace locked configuration")); }
                catch (IOException) { failed = true; }
                Expect(failed, "Replacing a configuration held by another reader must report failure");
            }
            byte[] afterFailure = File.ReadAllBytes(path);
            Expect(Convert.ToBase64String(original) == Convert.ToBase64String(afterFailure),
                "A failed replacement must leave the previous primary intact");
            Expect(!File.Exists(path + ".tmp"), "A failed replacement must clean up its temporary file");

            File.WriteAllText(path, "<broken>");
            File.WriteAllText(path + ".bak", "<also-broken>");
            string damagedPrimary = File.ReadAllText(path);
            string damagedBackup = File.ReadAllText(path + ".bak");
            loaded = SettingsStore.Load(path, out warning, out recovered);
            Expect(!recovered && loaded.Items.Count == 0 && !string.IsNullOrEmpty(warning),
                "When both copies are damaged, loading must return defaults with an explicit warning");
            Expect(File.ReadAllText(path) == damagedPrimary && File.ReadAllText(path + ".bak") == damagedBackup,
                "Damaged configuration files must be preserved for manual recovery");
            Console.WriteLine("PASS: atomic replacement, rolling backup, backup recovery, recovery-backup retention and failure preservation");
        }
        finally { Directory.Delete(root, true); }
    }
}
