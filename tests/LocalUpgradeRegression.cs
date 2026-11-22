using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using DeskDecor;

internal static class LocalUpgradeRegression
{
    private static string _root, _oldFixture, _currentFixture, _futureFixture;
    private static readonly Version Current = new Version(1, 1, 0, 0);
    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static string Copy(string name, string executable)
    {
        string path = Path.Combine(_root, name); Directory.CreateDirectory(path);
        File.Copy(executable, Path.Combine(path, "DeskDecor.exe")); return path;
    }
    private static string Xml(string directory) { return Path.Combine(directory, "DeskDecor.settings.xml"); }
    private static string Pending(string directory) { return Path.Combine(directory, ".migration", "pending-migration.xml"); }
    private static void Put(string path, string content)
    { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, content); }
    private static void Settings(string directory, string title, params string[] paths)
    {
        var settings = new AppSettings { DesktopInitialized = true };
        settings.Group.Title = title;
        if (paths.Length == 0) paths = new[] { Path.Combine(_root, "external-document.txt") };
        foreach (string path in paths) settings.Items.Add(new DesktopItem(path, 1, 2));
        using (var output = File.Create(Xml(directory))) new XmlSerializer(typeof(AppSettings)).Serialize(output, settings);
    }
    private static AppSettings Load(string path)
    { using (var input = File.OpenRead(path)) return (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(input); }
    private static LocalUpgradeRecord Record(string directory)
    { using (var input = File.OpenRead(Pending(directory))) return (LocalUpgradeRecord)new XmlSerializer(typeof(LocalUpgradeRecord)).Deserialize(input); }
    private static string Imported(string directory) { return Path.Combine(directory, Record(directory).ImportedDirectory); }
    private static LocalUpgradeCandidate Source(string directory) { return new LocalUpgradeCandidate(directory, false, 1); }
    private static LocalUpgradeResult Prepare(string current, params LocalUpgradeCandidate[] sources)
    { return LocalUpgrade.PrepareFromCandidates(current, Current, sources, path => false); }
    private static void Complete(string current) { LocalUpgrade.CompleteMigration(current, path => false); }
    private static bool BytesEqual(byte[] left, byte[] right)
    { return Convert.ToBase64String(left) == Convert.ToBase64String(right); }
    private static void NoArchive(string current)
    {
        Expect(Directory.GetDirectories(current, "migration-backup-*", SearchOption.AllDirectories).Length == 0,
            "Successful cleanup must not leave an old-version archive in the new directory");
        Expect(!File.Exists(Pending(current)), "Successful cleanup must remove its pending record");
    }

    private static void StandardCleanup()
    {
        string old = Copy("old-standard", _oldFixture), current = Copy("new-standard", _currentFixture);
        Settings(old, "user layout"); File.Copy(Xml(old), Xml(old) + ".bak");
        Put(Path.Combine(old, "DeskDecor.debug.log"), "old log");
        Put(Path.Combine(old, "README.md"), "old instructions");
        Put(Path.Combine(old, "DeskDecor.runtime", "ExplorerTAP-2026.2-x64", "ExplorerTAP.dll"), "synthetic runtime");
        Put(Path.Combine(old, ".migration", "migration-backup-old", "DeskDecor.settings.xml.bak"), "historic backup");
        byte[] primary = File.ReadAllBytes(Xml(old)), backup = File.ReadAllBytes(Xml(old) + ".bak");
        var result = Prepare(current, Source(old), new LocalUpgradeCandidate(old, false, 2));
        Expect(result.Imported && result.SourceDirectory == old, "One unique old location must be imported");
        Expect(BytesEqual(File.ReadAllBytes(Xml(current)), primary) && BytesEqual(File.ReadAllBytes(Xml(current) + ".bak"), backup),
            "Settings with external entries must remain byte-identical before cleanup");
        Expect(File.Exists(Path.Combine(old, "DeskDecor.exe")), "Old files must remain before healthy completion");
        Complete(current);
        Expect(!Directory.Exists(old), "All standard old program files, runtime, settings and historical backups must be deleted");
        Expect(BytesEqual(File.ReadAllBytes(Xml(current)), primary), "New settings must survive full old-directory deletion");
        Expect(!Directory.Exists(Path.Combine(current, "迁移文件")), "Unreferenced program-only files must not become retained user content");
        NoArchive(current);
        Console.WriteLine("PASS: standard old EXE, configuration, backups, logs, runtime and historical archives are removed without retaining a new archive");
    }

    private static void PersonalContent()
    {
        string old = Copy("old-content", _oldFixture), current = Copy("new-content", _currentFixture);
        string document = Path.Combine(old, "documents", "报告.txt"), empty = Path.Combine(old, "空文件夹");
        Put(document, "personal content"); Put(Path.Combine(old, "unlisted.bin"), "unlisted personal data");
        Put(Path.Combine(old, "README.md"), "referenced old README"); Directory.CreateDirectory(empty);
        Settings(old, "content", document, Path.GetDirectoryName(document), empty, Path.Combine(old, "README.md"), Path.Combine(old, "DeskDecor.exe"));
        File.Copy(Xml(old), Xml(old) + ".bak");
        Expect(Prepare(current, Source(old)).Imported, "Personal content must import with the settings");
        string imported = Imported(current);
        AppSettings settings = Load(Xml(current)), backup = Load(Xml(current) + ".bak");
        Expect(settings.Items.Count == 5 && settings.Items[0].Path == Path.Combine(imported, "documents", "报告.txt") &&
            settings.Items[1].Path == Path.Combine(imported, "documents") && settings.Items[2].Path == Path.Combine(imported, "空文件夹") &&
            settings.Items[3].Path == Path.Combine(imported, "README.md") && settings.Items[4].Path == Path.Combine(current, "DeskDecor.exe"),
            "Every direct old file/folder entry must map to preserved content, and the old EXE entry must point to the new executable");
        for (int i = 0; i < settings.Items.Count; i++) Expect(settings.Items[i].Path == backup.Items[i].Path, "Rolling backup entry paths must be migrated too");
        Expect(File.ReadAllText(settings.Items[0].Path) == "personal content" && Directory.Exists(settings.Items[2].Path) &&
            File.ReadAllText(settings.Items[3].Path) == "referenced old README" && File.ReadAllText(Path.Combine(imported, "unlisted.bin")) == "unlisted personal data",
            "Referenced program documents, unlisted files, nested folders and empty folders must be preserved before deletion");
        Complete(current);
        Expect(!Directory.Exists(old) && File.Exists(settings.Items[0].Path) && Directory.Exists(settings.Items[2].Path), "Old directory deletion must not break migrated entries");
        NoArchive(current);
        Console.WriteLine("PASS: personal files, nested and empty folders and referenced program documents migrate with primary/backup entry paths; old EXE entries use the new EXE");
    }

    private static void RecoveryAndDiscovery()
    {
        string old = Copy("old-damaged", _oldFixture), current = Copy("new-recovery", _currentFixture);
        Settings(old, "valid backup"); File.Copy(Xml(old), Xml(old) + ".bak"); File.WriteAllText(Xml(old), "<broken>");
        byte[] good = File.ReadAllBytes(Xml(old) + ".bak");
        Expect(Prepare(current, Source(old)).Imported && BytesEqual(File.ReadAllBytes(Xml(current)), good), "Valid backup must recover a damaged primary");
        Expect(File.ReadAllText(Xml(old)) == "<broken>", "Import must not repair old files in place");
        File.Delete(Xml(old)); current = Copy("new-missing-primary", _currentFixture);
        Expect(Prepare(current, Source(old)).Imported && BytesEqual(File.ReadAllBytes(Xml(current)), good) && !File.Exists(Xml(old)), "Missing source primary must recover read-only from backup");
        old = Copy("old-invalid", _oldFixture); Put(Xml(old), "<broken>"); Put(Xml(old) + ".bak", "<broken>");
        current = Copy("new-invalid", _currentFixture);
        Expect(!Prepare(current, Source(old)).Imported && !File.Exists(Xml(current)), "Two corrupt sources must be rejected");
        Console.WriteLine("PASS: damaged and missing primary configurations recover from valid backups without changing source files; two corrupt copies are rejected");

        old = Copy("old-active", _oldFixture); current = Copy("new-active", _currentFixture); Settings(old, "active");
        var result = Prepare(current, new LocalUpgradeCandidate(old, true, 3));
        Expect(result.NeedsOldExit && !result.Imported && !File.Exists(Xml(current)), "Running source must exit before import");
        result = LocalUpgrade.PrepareFromCandidates(current, Current, new[] { Source(old) }, path => true);
        Expect(result.NeedsOldExit && !File.Exists(Xml(current)), "A source that starts during discovery must block import");
        Expect(Prepare(current).Imported && Load(Xml(current)).Group.Title == "active", "Manual exit and relaunch must reuse a revalidated source hint");
        Expect(!File.Exists(Path.Combine(current, ".migration", "migration-source.xml")), "Successful import must clear the hint");
        current = Copy("new-multiple", _currentFixture); string second = Copy("old-second", _oldFixture); Settings(second, "second");
        result = Prepare(current, Source(old), new LocalUpgradeCandidate(second, false, 2));
        Expect(result.NeedsSourceSelection && result.SourceDirectories.Length == 2 && !File.Exists(Xml(current)), "Multiple valid source paths must require explicit selection");
        Settings(current, "untouched destination"); byte[] before = File.ReadAllBytes(Xml(current));
        Expect(!Prepare(current, Source(old)).Imported && BytesEqual(before, File.ReadAllBytes(Xml(current))), "Existing destination must remain unchanged");
        string backupDestination = Copy("new-existing-backup", _currentFixture); File.Copy(Xml(old), Xml(backupDestination) + ".bak");
        Expect(!Prepare(backupDestination, Source(second)).Imported && !File.Exists(Xml(backupDestination)), "Existing destination backup must be left to normal recovery");
        old = Copy("old-hint-invalid", _oldFixture); current = Copy("new-hint-invalid", _currentFixture); Settings(old, "invalidated hint");
        Prepare(current, new LocalUpgradeCandidate(old, true, 3)); File.WriteAllText(Xml(old), "<broken>");
        Expect(!Prepare(current).Imported && !File.Exists(Xml(current)), "Saved hint must not bypass revalidation");
        Console.WriteLine("PASS: running-source guards, relaunch hints, multi-source choice and existing-destination preservation remain intact");
    }

    private static void LockedCleanup()
    {
        string old = Copy("old-locked", _oldFixture), current = Copy("new-locked", _currentFixture);
        Settings(old, "locked runtime"); File.Copy(Xml(old), Xml(old) + ".bak");
        string locked = Path.Combine(old, "DeskDecor.runtime", "engine", "ExplorerTAP.dll"); Put(locked, "locked runtime bytes");
        Expect(Prepare(current, Source(old)).Imported, "Locked-runtime case must stage");
        using (var handle = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Complete(current);
            Expect(!File.Exists(Path.Combine(old, "DeskDecor.exe")) && !File.Exists(Xml(old)) && !File.Exists(Xml(old) + ".bak") && File.Exists(locked),
                "Cleanup must remove old EXE/settings even when one runtime file is in use");
            Expect(File.Exists(Pending(current)) && Record(current).CleanupStarted, "Partial cleanup must retain a resumable plan");
            Complete(current);
            Expect(File.Exists(locked) && File.Exists(Pending(current)), "Retry while locked must neither lose the file nor depend on removed EXE/XML");
        }
        Complete(current);
        Expect(!Directory.Exists(old), "Releasing the runtime lock must allow full cleanup on retry"); NoArchive(current);
        Console.WriteLine("PASS: locked runtime cleanup remains resumable after old EXE/XML deletion and finishes after the lock is released");
    }

    private static void ConflictingUpdates()
    {
        string old = Copy("old-changing", _oldFixture), current = Copy("new-changing", _currentFixture);
        string document = Path.Combine(old, "report.txt"); Put(document, "original report"); Settings(old, "changing data", document);
        Expect(Prepare(current, Source(old)).Imported, "Changing-file case must stage");
        string imported = Imported(current), destination = Load(Xml(current)).Items[0].Path;
        using (var handle = new FileStream(document, FileMode.Open, FileAccess.Read, FileShare.Read)) Complete(current);
        Expect(Record(current).CleanupStarted && File.Exists(document), "Source file must remain after delete lock");
        File.WriteAllText(document, "updated old report"); File.WriteAllText(destination, "user edited new report");
        Put(Path.Combine(old, "created-later", "later.txt"), "created during cleanup");
        for (int i = 0; i < 4 && Directory.Exists(old); i++) Complete(current);
        Expect(!Directory.Exists(old) && File.ReadAllText(destination) == "user edited new report", "Cleanup retry must not overwrite the new user's edits");
        var preserved = new List<string>(); foreach (string file in Directory.GetFiles(imported, "*", SearchOption.AllDirectories)) preserved.Add(File.ReadAllText(file));
        Expect(preserved.Contains("updated old report") && preserved.Contains("created during cleanup"), "Changed and newly created old files must survive as imported conflict copies");
        NoArchive(current);
        Console.WriteLine("PASS: cleanup preserves late old-file changes and new files as conflict copies without overwriting edits in the new directory");
    }

    private static void InheritedCleanup()
    {
        string old = Copy("old-inherited-a", _oldFixture), intermediate = Copy("new-inherited-b", _currentFixture);
        string document = Path.Combine(old, "documents", "retained.txt"); Put(document, "content survives two upgrades");
        string runtime = Path.Combine(old, "DeskDecor.runtime", "engine", "ExplorerTAP.dll"); Put(runtime, "inherited locked runtime");
        Settings(old, "inherited cleanup", document); File.Copy(Xml(old), Xml(old) + ".bak");
        Expect(Prepare(intermediate, Source(old)).Imported, "First upgrade must stage its personal content");
        string newest = Copy("new-inherited-c", _futureFixture);
        using (var handle = new FileStream(runtime, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Complete(intermediate);
            Expect(!File.Exists(Path.Combine(old, "DeskDecor.exe")) && !File.Exists(Xml(old)) && File.Exists(runtime) &&
                LocalUpgrade.HasPendingCleanup(intermediate), "First upgrade must retain only a resumable locked-runtime cleanup");
            var result = LocalUpgrade.PrepareFromCandidates(newest, new Version(2, 0, 0, 0), new[] { Source(intermediate) }, path => false);
            Expect(result.Imported, "A later version must import the intermediate installation");
            Complete(newest);
            string queue = Path.Combine(newest, ".migration", "cleanup-queue");
            Expect(!Directory.Exists(intermediate) && File.Exists(runtime) && Directory.Exists(queue) &&
                Directory.GetFiles(queue, "*.xml").Length == 1 && LocalUpgrade.HasPendingCleanup(newest),
                "Removing the intermediate directory must transfer the original runtime cleanup to the newest version's queue");
            AppSettings current = Load(Xml(newest));
            Expect(current.Items.Count == 1 && File.ReadAllText(current.Items[0].Path) == "content survives two upgrades",
                "Personal content and its entry path must remain readable after the intermediate directory is removed");
        }
        Complete(newest);
        Expect(!Directory.Exists(old) && !LocalUpgrade.HasPendingCleanup(newest) &&
            !Directory.Exists(Path.Combine(newest, ".migration", "cleanup-queue")) &&
            File.ReadAllText(Load(Xml(newest)).Items[0].Path) == "content survives two upgrades",
            "Releasing the inherited runtime lock must finish original-directory cleanup without harming twice-migrated content");
        NoArchive(newest);
        Console.WriteLine("PASS: a second upgrade inherits pending runtime cleanup, removes the intermediate version and finishes the original cleanup after unlock while retaining migrated data");
    }

    private static void ProtectedLocations()
    {
        string future = Copy("future", _futureFixture), same = Copy("same", _currentFixture), current = Copy("new-versions", _currentFixture);
        Settings(future, "future"); Settings(same, "same");
        Expect(LocalUpgrade.Discover(current, Current, new[] { Source(future), Source(same), Source(current) }).Count == 0, "Only older versions may migrate");
        string old = Copy("old-source-tree", _oldFixture); Settings(old, "source"); Put(Path.Combine(old, "Program.cs"), "// source");
        current = Copy("new-source-tree", _currentFixture); Expect(Prepare(current, Source(old)).Imported, "Source configuration may import"); Complete(current);
        Expect(File.Exists(Path.Combine(old, "DeskDecor.exe")) && File.Exists(Xml(old)), "Source tree must never be cleared");
        old = Copy("old-git", _oldFixture); Settings(old, "git"); Directory.CreateDirectory(Path.Combine(old, ".git"));
        current = Copy("new-git", _currentFixture); Prepare(current, Source(old)); Complete(current);
        Expect(File.Exists(Path.Combine(old, "DeskDecor.exe")), "Git workspace must not be removed");
        old = Copy("old-exe-change", _oldFixture); Settings(old, "changed executable"); current = Copy("new-exe-change", _currentFixture); Prepare(current, Source(old));
        File.AppendAllText(Path.Combine(old, "DeskDecor.exe"), "changed after import"); Complete(current);
        Expect(File.Exists(Path.Combine(old, "DeskDecor.exe")) && File.Exists(Xml(old)), "Changed old executable must prevent destructive cleanup");
        old = Copy("old-config-change", _oldFixture); Settings(old, "initial"); current = Copy("new-config-change", _currentFixture); Prepare(current, Source(old));
        Settings(old, "later source config"); Complete(current);
        Expect(File.Exists(Path.Combine(old, "DeskDecor.exe")), "Changed source settings before cleanup must prevent deletion");
        old = Copy("old-containing-new", _oldFixture); Settings(old, "parent"); current = Path.Combine(old, "new"); Directory.CreateDirectory(current); File.Copy(_currentFixture, Path.Combine(current, "DeskDecor.exe"));
        Prepare(current, Source(old)); Complete(current); Expect(File.Exists(Path.Combine(old, "DeskDecor.exe")), "Old parent containing the new copy must not be removed");
        current = Copy("new-containing-old", _currentFixture); old = Path.Combine(current, "old"); Directory.CreateDirectory(old); File.Copy(_oldFixture, Path.Combine(old, "DeskDecor.exe")); Settings(old, "child");
        Prepare(current, Source(old)); Complete(current); Expect(File.Exists(Path.Combine(old, "DeskDecor.exe")), "Nested source/destination directories must not be cleaned recursively");
        Console.WriteLine("PASS: version filtering, source/Git protection, changed source protection and both nested-directory arrangements prevent destructive cleanup");
    }

    private static void JunctionProtection()
    {
        string old = Copy("old-junction", _oldFixture), current = Copy("new-junction", _currentFixture), outside = Path.Combine(_root, "outside-junction");
        Settings(old, "junction"); Put(Path.Combine(outside, "keep.txt"), "outside target must survive"); string link = Path.Combine(old, "linked-folder");
        bool created = false;
        try
        {
            var start = new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + link + "\" \"" + outside + "\"") {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = Process.Start(start))
            { string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd(); process.WaitForExit(); created = process.ExitCode == 0; if (!created) Console.WriteLine("SKIP: junction creation unavailable: " + output.Trim()); }
            if (!created) return;
            bool refused = false; try { Prepare(current, Source(old)); } catch (IOException) { refused = true; }
            Complete(current);
            Expect((refused || !File.Exists(Xml(current))) && File.Exists(Path.Combine(old, "DeskDecor.exe")) && File.ReadAllText(Path.Combine(outside, "keep.txt")) == "outside target must survive",
                "A nested junction must not be followed or cause deletion of either source or its external target");
            Console.WriteLine("PASS: nested junction is rejected without following or deleting its outside target");
        }
        finally { if (created && Directory.Exists(link)) Directory.Delete(link, false); }
    }

    private static object ShellShortcut(object shell, string path)
    { return shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path }); }
    private static string Property(object value, string name)
    { return value.GetType().InvokeMember(name, BindingFlags.GetProperty, null, value, null) as string; }
    private static void ShortcutPaths()
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell", false);
        if (shellType == null) { Console.WriteLine("SKIP: WScript.Shell is not registered; shortcut migration not tested"); return; }
        object shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            string old = Copy("old-shortcuts", _oldFixture), current = Copy("new-shortcuts", _currentFixture);
            string target = Path.Combine(old, "assets", "target.txt"), link = Path.Combine(old, "document.lnk"); Put(target, "shortcut target");
            shortcut = ShellShortcut(shell, link);
            shortcut.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
            shortcut.GetType().InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(target) });
            shortcut.GetType().InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut,
                new object[] { "--input \"" + target + "\" --outside \"" + old + "-other\\keep.txt\"" });
            shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            Marshal.FinalReleaseComObject(shortcut); shortcut = null;
            Settings(old, "shortcut", link); File.Copy(Xml(old), Xml(old) + ".bak");
            Expect(Prepare(current, Source(old)).Imported, "Shortcut fixture must migrate"); string imported = Imported(current), migratedLink = Load(Xml(current)).Items[0].Path;
            shortcut = ShellShortcut(shell, migratedLink);
            Expect(string.Equals(Property(shortcut, "TargetPath"), Path.Combine(imported, "assets", "target.txt"), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Property(shortcut, "WorkingDirectory"), Path.Combine(imported, "assets"), StringComparison.OrdinalIgnoreCase),
                "Copied shortcut target and working directory must stop referring to the old directory");
            Expect(Property(shortcut, "Arguments").Contains("\"" + Path.Combine(imported, "assets", "target.txt") + "\"") &&
                Property(shortcut, "Arguments").Contains(old + "-other\\keep.txt"),
                "Shortcut arguments must rewrite old paths while preserving a different path that shares the old directory's prefix");
            Marshal.FinalReleaseComObject(shortcut); shortcut = null;
            Complete(current);
            Expect(!Directory.Exists(old) && File.ReadAllText(Path.Combine(imported, "assets", "target.txt")) == "shortcut target", "Migrated shortcut dependency must survive full source deletion"); NoArchive(current);
            Console.WriteLine("PASS: real internal shortcuts rewrite TargetPath, WorkingDirectory and path arguments without changing similarly prefixed outside paths");

            old = Copy("old-external-shortcut", _oldFixture); current = Copy("new-external-shortcut", _currentFixture);
            target = Path.Combine(old, "assets", "external-target.txt"); Put(target, "external shortcut dependency");
            link = Path.Combine(_root, "external-original.lnk");
            shortcut = ShellShortcut(shell, link);
            shortcut.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { Path.Combine(old, "DeskDecor.exe") });
            shortcut.GetType().InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(target) });
            shortcut.GetType().InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { "--input \"" + target + "\"" });
            shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            Marshal.FinalReleaseComObject(shortcut); shortcut = null;
            byte[] externalBefore = File.ReadAllBytes(link);
            Settings(old, "external shortcut", link); File.Copy(Xml(old), Xml(old) + ".bak");
            Expect(Prepare(current, Source(old)).Imported, "External shortcut referring to old content must migrate");
            imported = Imported(current); migratedLink = Load(Xml(current)).Items[0].Path;
            Expect(migratedLink != link && migratedLink.StartsWith(imported + "\\", StringComparison.OrdinalIgnoreCase) &&
                Load(Xml(current) + ".bak").Items[0].Path == migratedLink && BytesEqual(externalBefore, File.ReadAllBytes(link)),
                "Primary and backup must use a repaired private shortcut copy; the original external shortcut must not change");
            shortcut = ShellShortcut(shell, migratedLink);
            Expect(string.Equals(Property(shortcut, "TargetPath"), Path.Combine(current, "DeskDecor.exe"), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Property(shortcut, "WorkingDirectory"), Path.Combine(imported, "assets"), StringComparison.OrdinalIgnoreCase) &&
                Property(shortcut, "Arguments").Contains(Path.Combine(imported, "assets", "external-target.txt")),
                "External shortcut copies must target the new EXE and imported working directory/argument files");
            Marshal.FinalReleaseComObject(shortcut); shortcut = null;
            Complete(current);
            Expect(!Directory.Exists(old) && BytesEqual(externalBefore, File.ReadAllBytes(link)) &&
                File.ReadAllText(Path.Combine(imported, "assets", "external-target.txt")) == "external shortcut dependency",
                "External shortcut repair must preserve original external data while removing the complete old directory");
            NoArchive(current);
            Console.WriteLine("PASS: external shortcuts are repaired in copied entries, leaving their original files untouched while preserving old-directory dependencies");

            old = Copy("old-owned-shortcut-dependency", _oldFixture); current = Copy("new-owned-shortcut-dependency", _currentFixture);
            string runtime = Path.Combine(old, "DeskDecor.runtime", "engine");
            Put(Path.Combine(runtime, "required.dll"), "referenced runtime dependency");
            Put(Path.Combine(old, "README.md"), "referenced program document");
            link = Path.Combine(old, "read-program-document.lnk");
            shortcut = ShellShortcut(shell, link);
            shortcut.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { Path.Combine(Environment.SystemDirectory, "notepad.exe") });
            shortcut.GetType().InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { runtime });
            shortcut.GetType().InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { "\"" + Path.Combine(old, "README.md") + "\"" });
            shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            Marshal.FinalReleaseComObject(shortcut); shortcut = null;
            Settings(old, "owned shortcut dependencies", link);
            Expect(Prepare(current, Source(old)).Imported, "Program-owned dependencies referenced by shortcut metadata must migrate");
            imported = Imported(current); migratedLink = Load(Xml(current)).Items[0].Path;
            shortcut = ShellShortcut(shell, migratedLink);
            Expect(Property(shortcut, "Arguments").Contains(Path.Combine(imported, "README.md")) &&
                string.Equals(Property(shortcut, "WorkingDirectory"), Path.Combine(imported, "DeskDecor.runtime", "engine"), StringComparison.OrdinalIgnoreCase),
                "Argument and working-directory references must use the migrated dependencies");
            Marshal.FinalReleaseComObject(shortcut); shortcut = null;
            LocalUpgrade.CompleteMigration(current, path => false);
            Expect(!Directory.Exists(old) && File.ReadAllText(Path.Combine(imported, "README.md")) == "referenced program document" &&
                File.ReadAllText(Path.Combine(imported, "DeskDecor.runtime", "engine", "required.dll")) == "referenced runtime dependency",
                "Referenced program-owned files must be copied before full source cleanup");
            NoArchive(current);
            Console.WriteLine("PASS: shortcut arguments and directory metadata preserve referenced program-owned files before cleanup");
        }
        finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); if (shell != null) Marshal.FinalReleaseComObject(shell); }
    }

    [STAThread] public static void Main(string[] args)
    {
        _oldFixture = args[0]; _currentFixture = args[1]; _futureFixture = args[2];
        _root = Path.Combine(Path.GetTempPath(), "DeskDecor-local-upgrade-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_root);
        try
        {
            Put(Path.Combine(_root, "external-document.txt"), "external original");
            StandardCleanup(); PersonalContent(); RecoveryAndDiscovery(); LockedCleanup(); InheritedCleanup(); ConflictingUpdates(); ProtectedLocations(); JunctionProtection(); ShortcutPaths();
            string current = Copy("registration", _currentFixture), registration = Path.Combine(_root, "records", "installation.xml");
            LocalUpgrade.RecordSuccessfulInstall(current, registration);
            Expect(File.ReadAllText(registration).Contains(current), "Successful-install registration must store the healthy directory");
            Expect(LocalUpgrade.ParseStartupPath("\"C:\\My Apps\\DeskDecor.exe\" --argument") == "C:\\My Apps\\DeskDecor.exe", "Quoted startup paths must be understood");
            Expect(LocalUpgrade.ParseStartupPath("C:\\My Apps\\Other.exe") == null, "Unrelated startup executables must be excluded");
            Console.WriteLine("PASS: isolated successful-install registration and startup command parsing remain intact");
        }
        finally
        {
            string resolved = Path.GetFullPath(_root);
            if (Path.GetDirectoryName(resolved).TrimEnd('\\') != Path.GetTempPath().TrimEnd('\\') || !Path.GetFileName(resolved).StartsWith("DeskDecor-local-upgrade-", StringComparison.Ordinal)) throw new Exception("Unexpected test cleanup path");
            Directory.Delete(resolved, true);
        }
    }
}
