using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace DeskDecor
{
    internal sealed class LocalUpgradeResult
    {
        public string SourceDirectory;
        public string[] SourceDirectories = new string[0];
        public bool Imported;
        public bool NeedsOldExit;
        public bool ExitRequested;
        public bool NeedsSourceSelection;
        public bool Failed;
        public string Message;
    }

    internal sealed class LocalUpgradeCandidate
    {
        public string Directory;
        public bool Running;
        public int Priority;
        public LocalUpgradeCandidate(string directory, bool running, int priority)
        { Directory = directory; Running = running; Priority = priority; }
    }

    public sealed class LocalUpgradeRecord
    {
        public string SourceDirectory;
        public string SourceVersion;
        public string SourceExeHash;
        public string SourceSettingsHash;
        public string SourceBackupHash;
        public string DestinationExeHash;
        public bool CleanupStarted;
        public List<LocalUpgradeCleanupFile> CleanupFiles;
        public List<string> CleanupDirectories;
        public string ImportedDirectory;
        public List<LocalUpgradeExternalLink> ExternalLinks;
    }

    public sealed class LocalUpgradeExternalLink
    {
        public string OriginalPath;
        public string ImportedPath;
        public string Hash;
    }

    public sealed class LocalUpgradeCleanupFile
    {
        public string Path;
        public string Hash;
        public string ImportedPath;
        public string ImportedHash;
    }

    internal static class LocalUpgrade
    {
        public const string PrepareEventName = "Local\\DeskDecor.PrepareUpgrade.20261122";
        private const string ExeName = "DeskDecor.exe";
        private const string SettingsName = "DeskDecor.settings.xml";
        private const long MaximumSettingsBytes = 8L * 1024 * 1024;
        private static readonly string[] ProgramFiles = {
            ExeName, SettingsName, "DeskDecor.ico", "DeskDecor.debug.log", "DeskDecor.startup.log",
            "DeskDecor.update.log", "DeskDecor.shell.txt", "DeskDecor.surface.png", "README.md", "README.en.md",
            "LICENSE.md", "THIRD-PARTY-NOTICES.md", "DeskDecor-source.zip", "DeskDecor-portable-x64.zip"
        };

        public static LocalUpgradeResult Prepare(string currentDirectory, Version currentVersion)
        { return Prepare(currentDirectory, currentVersion, null); }

        public static LocalUpgradeResult Prepare(string currentDirectory, Version currentVersion, string explicitSourceDirectory)
        {
            try
            {
                List<LocalUpgradeCandidate> candidates = FindCandidates(RegistrationPath());
                if (!string.IsNullOrWhiteSpace(explicitSourceDirectory))
                {
                    string selected = Path.GetFullPath(explicitSourceDirectory);
                    if (File.Exists(selected)) selected = Path.GetDirectoryName(selected);
                    candidates = new List<LocalUpgradeCandidate> {
                        new LocalUpgradeCandidate(selected, IsRunning(selected), 3) };
                }
                LocalUpgradeResult result = PrepareFromCandidates(currentDirectory, currentVersion, candidates, IsRunning,
                    string.IsNullOrWhiteSpace(explicitSourceDirectory));
                if (result.NeedsOldExit)
                {
                    try
                    {
                        using (EventWaitHandle handle = EventWaitHandle.OpenExisting(PrepareEventName))
                        { handle.Set(); result.ExitRequested = true; }
                        result.Message = "已通知旧版保存并退出，请稍候重新打开新版，配置将自动迁移。";
                    }
                    catch
                    { result.Message = "旧版正在运行且不支持自动退出。请先从旧版系统托盘选择“退出”，然后重新打开新版。"; }
                }
                if (explicitSourceDirectory != null && !result.Imported && !result.NeedsOldExit &&
                    !result.NeedsSourceSelection && !File.Exists(Path.Combine(currentDirectory, SettingsName)))
                {
                    result.Failed = true;
                    result.Message = "所选目录没有可迁移的旧版 DeskDecor 和有效配置。";
                }
                return result;
            }
            catch (Exception ex)
            { return new LocalUpgradeResult { Failed = true, Message = "旧配置迁移未完成，原文件已保留：" + ex.Message }; }
        }

        internal static LocalUpgradeResult PrepareFromCandidates(string currentDirectory, Version currentVersion,
            IEnumerable<LocalUpgradeCandidate> candidates, Func<string, bool> isRunning, bool useSavedSourceHint = true)
        {
            string root = SafeDirectory(currentDirectory);
            if (File.Exists(Path.Combine(root, SettingsName)) || File.Exists(Path.Combine(root, SettingsName + ".bak")))
                return new LocalUpgradeResult();
            if (currentVersion == null) throw new ArgumentNullException("currentVersion");
            List<LocalUpgradeCandidate> considered = new List<LocalUpgradeCandidate>(candidates);
            string sourceHint = Path.Combine(root, ".migration", "migration-source.xml");
            if (useSavedSourceHint && File.Exists(sourceHint))
            {
                try
                {
                    SafeDirectory(Path.GetDirectoryName(sourceHint));
                    LocalInstallation remembered = ReadXml<LocalInstallation>(sourceHint);
                    considered.Add(new LocalUpgradeCandidate(remembered.Directory, false, 4));
                }
                catch { }
            }
            List<LocalUpgradeCandidate> usable = Discover(root, currentVersion, considered);
            if (usable.Count == 0) return new LocalUpgradeResult();
            if (usable.Count > 1)
            {
                List<string> directories = new List<string>();
                foreach (LocalUpgradeCandidate candidate in usable) directories.Add(candidate.Directory);
                return new LocalUpgradeResult { NeedsSourceSelection = true, SourceDirectories = directories.ToArray(),
                    Message = "发现多份旧配置，请选择需要保留的旧版 DeskDecor.exe 或其所在目录。" };
            }
            LocalUpgradeCandidate source = usable[0];
            LocalUpgradeResult result = new LocalUpgradeResult { SourceDirectory = source.Directory };
            // Early releases cannot handle the exit request and have no install
            // registration. Remember this verified location across the user's
            // manual exit/relaunch; discovery validates it again on every use.
            string updates = SafeUpdates(root);
            WriteXml(sourceHint, new LocalInstallation { Directory = source.Directory });
            if (source.Running || isRunning(source.Directory))
            { result.NeedsOldExit = true; return result; }
            string sourceExe = Path.Combine(source.Directory, ExeName);
            string sourceSettings = Path.Combine(source.Directory, SettingsName);
            string sourceBackup = sourceSettings + ".bak";
            byte[] primaryBytes = ReadOptional(sourceSettings);
            byte[] backupBytes = ReadOptional(sourceBackup);
            byte[] imported = IsValidSettings(primaryBytes) ? primaryBytes : backupBytes;
            if (!IsValidSettings(imported)) throw new InvalidDataException("旧版配置和备份均无法读取。");
            // Recheck immediately before copying; a running old copy owns its own
            // in-memory settings and may still save them as it exits.
            if (isRunning(source.Directory)) { result.NeedsOldExit = true; return result; }
            LocalUpgradeRecord record = new LocalUpgradeRecord {
                SourceDirectory = source.Directory,
                SourceVersion = ReadVersion(sourceExe).ToString(),
                SourceExeHash = HashFile(sourceExe),
                SourceSettingsHash = HashBytes(primaryBytes),
                SourceBackupHash = HashBytes(backupBytes),
                DestinationExeHash = HashFile(Path.Combine(root, ExeName))
            };
            PrepareContentMigration(root, source.Directory, record, imported, backupBytes);
            imported = RewriteContentPaths(root, source.Directory, record, imported);
            byte[] importedBackup = IsValidSettings(backupBytes)
                ? RewriteContentPaths(root, source.Directory, record, backupBytes) : null;
            string destination = Path.Combine(root, SettingsName);
            string temp = Path.Combine(updates, "migration-settings-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { output.Write(imported, 0, imported.Length); output.Flush(true); }
                if (HashFile(temp) != HashBytes(imported)) throw new IOException("配置复制校验失败。");
                if (File.Exists(destination) || File.Exists(destination + ".bak")) return new LocalUpgradeResult();
                // Preserve a valid rolling backup when present. Never replace a
                // configuration created by another launch in the meantime.
                if (importedBackup != null) WriteNew(destination + ".bak", importedBackup);
                File.Move(temp, destination);
                WriteXml(Path.Combine(updates, "pending-migration.xml"), record);
                File.Delete(sourceHint);
                result.Imported = true;
                result.Message = "已迁移旧版配置，正常启动后将清理旧版程序、配置和备份。";
                return result;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        internal static List<LocalUpgradeCandidate> Discover(string currentDirectory, Version currentVersion,
            IEnumerable<LocalUpgradeCandidate> candidates)
        {
            List<LocalUpgradeCandidate> valid = new List<LocalUpgradeCandidate>();
            foreach (LocalUpgradeCandidate candidate in candidates)
            {
                try
                {
                    string directory = SafeDirectory(candidate.Directory);
                    if (SamePath(directory, currentDirectory)) continue;
                    Version version = ReadVersion(Path.Combine(directory, ExeName));
                    if (version >= currentVersion) continue;
                    byte[] primary = ReadOptional(Path.Combine(directory, SettingsName));
                    byte[] backup = ReadOptional(Path.Combine(directory, SettingsName + ".bak"));
                    if (!IsValidSettings(primary) && !IsValidSettings(backup)) continue;
                    LocalUpgradeCandidate existing = valid.Find(c => SamePath(c.Directory, directory));
                    if (existing != null)
                    { existing.Running |= candidate.Running; existing.Priority = Math.Max(existing.Priority, candidate.Priority); }
                    else valid.Add(new LocalUpgradeCandidate(directory, candidate.Running, candidate.Priority));
                }
                catch { }
            }
            valid.Sort((left, right) => right.Priority.CompareTo(left.Priority));
            return valid;
        }

        // Call only after healthy startup. The saved, hash-checked cleanup plan
        // survives a locked DLL or interruption after the old EXE was removed.
        public static void CompleteMigration(string currentDirectory)
        { CompleteMigration(currentDirectory, IsRunning); }

        internal static void CompleteMigration(string currentDirectory, Func<string, bool> isRunning)
        {
            CompleteMigrationRecord(currentDirectory, isRunning, null);
            string queue = Path.Combine(currentDirectory, ".migration", "cleanup-queue");
            try
            {
                if (!Directory.Exists(queue)) return;
                SafeDirectory(queue);
                foreach (string record in Directory.GetFiles(queue, "*.xml")) CompleteMigrationRecord(currentDirectory, isRunning, record);
                DeleteEmptyDirectory(queue);
                DeleteEmptyDirectory(Path.GetDirectoryName(queue));
            }
            catch { }
        }

        public static bool HasPendingCleanup(string currentDirectory)
        {
            if (File.Exists(Path.Combine(currentDirectory, ".migration", "pending-migration.xml"))) return true;
            try
            {
                string queue = Path.Combine(currentDirectory, ".migration", "cleanup-queue");
                return Directory.Exists(queue) && Directory.GetFiles(queue, "*.xml").Length > 0;
            }
            catch { return false; }
        }

        private static void CompleteMigrationRecord(string currentDirectory, Func<string, bool> isRunning, string queuedRecord)
        {
            try
            {
                string root = SafeDirectory(currentDirectory);
                string updates = Path.Combine(root, ".migration");
                string pending = queuedRecord ?? Path.Combine(updates, "pending-migration.xml");
                if (!File.Exists(pending)) return;
                SafeDirectory(updates);
                LocalUpgradeRecord record = ReadXml<LocalUpgradeRecord>(pending);
                string source = Path.GetFullPath(record.SourceDirectory).TrimEnd(Path.DirectorySeparatorChar);
                string sourceExe = Path.Combine(source, ExeName);
                if (SamePath(source, root) || IsWithin(root, source) || IsWithin(source, root) ||
                    IsPersonalDirectory(source) || isRunning(source)) return;
                if (Directory.Exists(source))
                {
                    SafeDirectory(source);
                    if (IsSourceTree(source)) return;
                }
                if (HashFile(Path.Combine(root, ExeName)) != record.DestinationExeHash ||
                    !IsValidSettings(ReadOptional(Path.Combine(root, SettingsName)))) return;
                ValidateCleanupPlan(source, record);
                if (!record.CleanupStarted)
                {
                    if (ReadVersion(sourceExe).ToString() != record.SourceVersion ||
                        ReadVersion(sourceExe) >= ReadVersion(Path.Combine(root, ExeName)) ||
                        HashFile(sourceExe) != record.SourceExeHash) return;
                    byte[] primary = ReadOptional(Path.Combine(source, SettingsName));
                    byte[] backup = ReadOptional(Path.Combine(source, SettingsName + ".bak"));
                    if (HashBytes(primary) != record.SourceSettingsHash || HashBytes(backup) != record.SourceBackupHash) return;
                    foreach (LocalUpgradeCleanupFile file in record.CleanupFiles)
                    {
                        string actualHash = HashFile(Path.Combine(source, file.Path));
                        if (actualHash != file.Hash)
                        {
                            if (IsExe(file.Path)) return;
                            CopyImportedFile(root, source, record, file, ConflictRelative(file.Path, actualHash), actualHash);
                            file.Hash = actualHash;
                        }
                        if (file.ImportedPath != null)
                        {
                            string imported = ImportPath(root, record, file.ImportedPath);
                            if (!File.Exists(imported) || HashFile(imported) != file.ImportedHash)
                                CopyImportedFile(root, source, record, file, ConflictRelative(file.Path, file.Hash), file.Hash);
                        }
                    }
                    if (record.ExternalLinks != null)
                        foreach (LocalUpgradeExternalLink link in record.ExternalLinks)
                            if (HashFile(ImportPath(root, record, link.ImportedPath)) != link.Hash) return;
                    // The executable goes first, so an interrupted cleanup
                    // cannot leave a launchable app with half its settings gone.
                    record.CleanupFiles.Sort((a, b) => (IsExe(a.Path) ? 0 : 1).CompareTo(IsExe(b.Path) ? 0 : 1));
                    record.CleanupStarted = true;
                    WriteXml(pending, record);
                }
                bool complete = true;
                byte[] latestSettings = ReadOptional(Path.Combine(root, SettingsName));
                foreach (DesktopItem item in ReadSettings(latestSettings).Items)
                    if (SamePath(item.Path, source) || IsWithin(item.Path, source)) return;
                foreach (LocalUpgradeCleanupFile file in record.CleanupFiles)
                {
                    string path = Path.Combine(source, file.Path);
                    if (!File.Exists(path)) continue;
                    try
                    {
                        SafeDirectory(Path.GetDirectoryName(path));
                        RejectReparse(path);
                        // Deny modification while comparing and deleting the
                        // exact file, but allow Windows to unlink this handle.
                        using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                        {
                            string actualHash = HashStream(input);
                            if (actualHash != file.Hash)
                            {
                                if (IsExe(file.Path)) return;
                                // Preserve post-migration changes separately;
                                // never overwrite a file already used in the new app.
                                string conflict = ConflictRelative(file.Path, actualHash);
                                CopyImportedFile(root, source, record, file, conflict, actualHash);
                                file.Hash = actualHash;
                                WriteXml(pending, record);
                            }
                            if (isRunning(source)) return;
                            FileAttributes attributes = File.GetAttributes(path);
                            if ((attributes & FileAttributes.ReadOnly) != 0) File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                            File.Delete(path);
                        }
                    }
                    catch (IOException) { complete = false; }
                    catch (UnauthorizedAccessException) { complete = false; }
                    // Never remove configuration while a locked old executable
                    // could still be launched by the user.
                    if (IsExe(file.Path) && File.Exists(path)) return;
                }
                // Files created while cleanup was waiting must be preserved too.
                if (Directory.Exists(source))
                {
                    List<string> remainingFiles = new List<string>();
                    List<string> remainingDirectories = new List<string>();
                    EnumerateContent(source, source, remainingFiles, remainingDirectories);
                    foreach (string relative in remainingFiles)
                    {
                        if (record.CleanupFiles.Exists(f => string.Equals(f.Path, relative, StringComparison.OrdinalIgnoreCase))) continue;
                        if (IsExe(relative)) return;
                        string hash = HashFile(Path.Combine(source, relative));
                        LocalUpgradeCleanupFile extra = new LocalUpgradeCleanupFile { Path = relative, Hash = hash };
                        CopyImportedFile(root, source, record, extra, ConflictRelative(relative, hash), hash);
                        record.CleanupFiles.Add(extra);
                        complete = false;
                    }
                    foreach (string relative in remainingDirectories)
                        if (!record.CleanupDirectories.Contains(relative)) record.CleanupDirectories.Add(relative);
                    if (!complete) WriteXml(pending, record);
                }
                record.CleanupDirectories.Sort((a, b) => b.Length.CompareTo(a.Length));
                foreach (string relative in record.CleanupDirectories)
                    DeleteEmptyDirectory(Path.Combine(source, relative));
                if (complete)
                {
                    DeleteEmptyDirectory(source);
                    if (Directory.Exists(source)) return;
                    File.Delete(pending);
                    DeleteEmptyDirectory(updates);
                }
            }
            catch { /* Leave the fixed cleanup plan for a later retry. */ }
        }

        private static bool IsExe(string relative)
        { return string.Equals(relative, ExeName, StringComparison.OrdinalIgnoreCase); }

        private static void PrepareContentMigration(string root, string source, LocalUpgradeRecord record,
            byte[] primary, byte[] backup)
        {
            if (SamePath(root, source) || IsWithin(root, source) || IsWithin(source, root) ||
                IsPersonalDirectory(source) || IsSourceTree(source)) return;
            List<string> files = new List<string>();
            List<string> directories = new List<string>();
            EnumerateContent(source, source, files, directories);
            record.CleanupFiles = new List<LocalUpgradeCleanupFile>();
            record.CleanupDirectories = directories;
            foreach (string relative in files)
            {
                LocalUpgradeCleanupFile file = new LocalUpgradeCleanupFile { Path = relative, Hash = HashFile(Path.Combine(source, relative)) };
                record.CleanupFiles.Add(file);
                if (!IsExe(relative) && (!IsOwnedPath(relative) || ReferencesPath(primary, Path.Combine(source, relative), source) ||
                    ReferencesPath(backup, Path.Combine(source, relative), source)))
                    CopyImportedFile(root, source, record, file, relative, file.Hash);
            }
            foreach (string relative in directories)
                if (!IsOwnedPath(relative) || ReferencesPath(primary, Path.Combine(source, relative), source) ||
                    ReferencesPath(backup, Path.Combine(source, relative), source))
                    EnsureImportDirectory(root, record, relative);
            foreach (LocalUpgradeCleanupFile file in record.CleanupFiles)
                if (file.ImportedPath != null && string.Equals(Path.GetExtension(file.Path), ".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string destination = ImportPath(root, record, file.ImportedPath);
                    RewriteShortcut(destination, root, source, record);
                    file.ImportedHash = HashFile(destination);
                }
            record.ExternalLinks = new List<LocalUpgradeExternalLink>();
            foreach (byte[] configuration in new[] { primary, backup })
            {
                AppSettings settings;
                try { settings = ReadSettings(configuration); } catch { continue; }
                if (settings == null) continue;
                foreach (DesktopItem item in settings.Items)
                {
                    if (!string.Equals(Path.GetExtension(item.Path), ".lnk", StringComparison.OrdinalIgnoreCase) ||
                        IsWithin(item.Path, source) || !File.Exists(item.Path) ||
                        record.ExternalLinks.Exists(link => SamePath(link.OriginalPath, item.Path)) || !ShortcutReferences(item.Path, source)) continue;
                    string relative = Path.Combine("外部快捷方式", HashFile(item.Path).Substring(0, 12), Path.GetFileName(item.Path));
                    EnsureImportDirectory(root, record, Path.GetDirectoryName(relative));
                    string destination = ImportPath(root, record, relative);
                    if (!File.Exists(destination))
                    {
                        using (FileStream input = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        { input.CopyTo(output); output.Flush(true); }
                    }
                    RewriteShortcut(destination, root, source, record);
                    record.ExternalLinks.Add(new LocalUpgradeExternalLink { OriginalPath = item.Path, ImportedPath = relative, Hash = HashFile(destination) });
                }
            }
            TransferPendingCleanup(root, source, record);
        }

        private static void TransferPendingCleanup(string root, string source, LocalUpgradeRecord migration)
        {
            List<string> pending = new List<string>();
            string previous = Path.Combine(source, ".migration", "pending-migration.xml");
            if (File.Exists(previous)) pending.Add(previous);
            string previousQueue = Path.Combine(source, ".migration", "cleanup-queue");
            if (Directory.Exists(previousQueue)) pending.AddRange(Directory.GetFiles(previousQueue, "*.xml"));
            foreach (string path in pending)
            {
                LocalUpgradeRecord record = ReadXml<LocalUpgradeRecord>(path);
                string original = Path.GetFullPath(record.SourceDirectory).TrimEnd(Path.DirectorySeparatorChar);
                if (!Directory.Exists(original)) continue;
                SafeDirectory(original);
                if (SamePath(original, source) || SamePath(original, root) || IsWithin(root, original) || IsWithin(original, root) ||
                    IsPersonalDirectory(original) || IsSourceTree(original)) continue;
                if (record.CleanupFiles == null || record.CleanupDirectories == null) continue;
                ValidateCleanupPlan(original, record);
                if (record.ImportedDirectory != null)
                {
                    EnsureImportDirectory(root, migration, "");
                    record.ImportedDirectory = Path.Combine(migration.ImportedDirectory, record.ImportedDirectory);
                    foreach (LocalUpgradeCleanupFile file in record.CleanupFiles)
                        if (file.ImportedPath != null && File.Exists(ImportPath(root, record, file.ImportedPath)))
                            file.ImportedHash = HashFile(ImportPath(root, record, file.ImportedPath));
                    if (record.ExternalLinks != null)
                        foreach (LocalUpgradeExternalLink link in record.ExternalLinks)
                            link.Hash = HashFile(ImportPath(root, record, link.ImportedPath));
                }
                record.DestinationExeHash = migration.DestinationExeHash;
                string queue = Path.Combine(SafeUpdates(root), "cleanup-queue");
                Directory.CreateDirectory(queue);
                SafeDirectory(queue);
                WriteXml(Path.Combine(queue, HashBytes(System.Text.Encoding.UTF8.GetBytes(original)).Substring(0, 20) + ".xml"), record);
            }
        }

        private static void EnumerateContent(string source, string directory, List<string> files, List<string> directories)
        {
            SafeDirectory(directory);
            foreach (string file in Directory.GetFiles(directory))
            {
                RejectReparse(file);
                files.Add(file.Substring(source.Length + 1));
                if (files.Count > 20000) throw new InvalidDataException("旧目录文件过多，已保留原文件。");
            }
            foreach (string child in Directory.GetDirectories(directory))
            {
                SafeDirectory(child);
                directories.Add(child.Substring(source.Length + 1));
                EnumerateContent(source, child, files, directories);
            }
        }

        private static bool IsOwnedPath(string relative)
        {
            return relative.Equals("DeskDecor.runtime", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("DeskDecor.runtime\\", StringComparison.OrdinalIgnoreCase) ||
                relative.Equals(".migration", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith(".migration\\", StringComparison.OrdinalIgnoreCase) ||
                (relative.IndexOf('\\') < 0 && IsProgramFile(relative));
        }

        private static string EnsureImportDirectory(string root, LocalUpgradeRecord record, string relative)
        {
            if (record.ImportedDirectory == null)
            {
                string fingerprint = string.IsNullOrEmpty(record.SourceSettingsHash) ? record.SourceBackupHash : record.SourceSettingsHash;
                if (string.IsNullOrEmpty(fingerprint)) fingerprint = record.SourceExeHash;
                record.ImportedDirectory = Path.Combine("迁移文件", Path.GetFileName(record.SourceDirectory) + "-" + fingerprint.Substring(0, 8).ToLowerInvariant());
            }
            string destination = ImportPath(root, record, relative);
            Directory.CreateDirectory(destination);
            SafeDirectory(destination);
            return destination;
        }

        private static string ImportPath(string root, LocalUpgradeRecord record, string relative)
        {
            if (record.ImportedDirectory == null || !record.ImportedDirectory.StartsWith("迁移文件\\", StringComparison.Ordinal))
                throw new InvalidDataException("迁移文件目录无效。");
            ValidateRelativePath(record.ImportedDirectory);
            if (!string.IsNullOrEmpty(relative)) ValidateRelativePath(relative);
            string directory = Path.Combine(root, record.ImportedDirectory);
            string destination = string.IsNullOrEmpty(relative) ? directory : Path.Combine(directory, relative);
            if (!IsWithin(destination, root)) throw new InvalidDataException("迁移文件目录超出范围。");
            return destination;
        }

        private static void CopyImportedFile(string root, string source, LocalUpgradeRecord record,
            LocalUpgradeCleanupFile file, string importedRelative, string hash)
        {
            EnsureImportDirectory(root, record, Path.GetDirectoryName(importedRelative) ?? "");
            string destination = ImportPath(root, record, importedRelative);
            if (File.Exists(destination))
            {
                if (HashFile(destination) != hash) throw new IOException("新版迁移文件已存在且内容不同，原文件已保留。");
            }
            else
            {
                using (FileStream input = new FileStream(Path.Combine(source, file.Path), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                {
                    if (HashStream(input) != hash) throw new IOException("旧文件在迁移期间发生变化。");
                    input.Position = 0;
                    using (FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { input.CopyTo(output); output.Flush(true); }
                }
                if (HashFile(destination) != hash) throw new IOException("迁移文件校验失败。");
            }
            file.ImportedPath = importedRelative;
            file.ImportedHash = hash;
        }

        private static string ConflictRelative(string relative, string hash)
        { return Path.Combine("迁移期间更新的文件", hash.Substring(0, 12), relative); }

        private static byte[] RewriteContentPaths(string root, string source, LocalUpgradeRecord record, byte[] bytes)
        {
            if (record.CleanupFiles == null || bytes == null) return bytes;
            AppSettings settings = ReadSettings(bytes);
            bool changed = false;
            foreach (DesktopItem item in settings.Items)
            {
                LocalUpgradeExternalLink link = record.ExternalLinks == null ? null : record.ExternalLinks.Find(value => SamePath(value.OriginalPath, item.Path));
                if (link != null) { item.Path = ImportPath(root, record, link.ImportedPath); changed = true; continue; }
                if (!SamePath(item.Path, source) && !IsWithin(item.Path, source)) continue;
                if (SamePath(item.Path, Path.Combine(source, ExeName))) item.Path = Path.Combine(root, ExeName);
                else
                {
                    EnsureImportDirectory(root, record, "");
                    string relative = SamePath(item.Path, source) ? "" : Path.GetFullPath(item.Path).Substring(source.Length + 1);
                    item.Path = ImportPath(root, record, relative);
                }
                changed = true;
            }
            if (!changed) return bytes;
            using (MemoryStream output = new MemoryStream())
            { new XmlSerializer(typeof(AppSettings)).Serialize(output, settings); return output.ToArray(); }
        }

        private static void RewriteShortcut(string path, string root, string source, LocalUpgradeRecord record)
        {
            object shell = null, shortcut = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
                shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                bool changed = false;
                foreach (string property in new[] { "TargetPath", "WorkingDirectory" })
                {
                    string value = shortcut.GetType().InvokeMember(property, BindingFlags.GetProperty, null, shortcut, null) as string;
                    if (string.IsNullOrEmpty(value) || !Path.IsPathRooted(value) || (!SamePath(value, source) && !IsWithin(value, source))) continue;
                    string mapped;
                    if (SamePath(value, Path.Combine(source, ExeName))) mapped = Path.Combine(root, ExeName);
                    else
                    {
                        EnsureImportDirectory(root, record, "");
                        string relative = SamePath(value, source) ? "" : Path.GetFullPath(value).Substring(source.Length + 1);
                        mapped = ImportPath(root, record, relative);
                        ImportShortcutDependency(root, source, record, value);
                    }
                    shortcut.GetType().InvokeMember(property, BindingFlags.SetProperty, null, shortcut, new object[] { mapped });
                    changed = true;
                }
                string arguments = shortcut.GetType().InvokeMember("Arguments", BindingFlags.GetProperty, null, shortcut, null) as string;
                if (!string.IsNullOrEmpty(arguments) && arguments.IndexOf(source, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    EnsureImportDirectory(root, record, "");
                    foreach (LocalUpgradeCleanupFile file in record.CleanupFiles)
                        if (ContainsArgumentPath(arguments, Path.Combine(source, file.Path)))
                            ImportShortcutDependency(root, source, record, Path.Combine(source, file.Path));
                    foreach (string directory in record.CleanupDirectories)
                        if (ContainsArgumentPath(arguments, Path.Combine(source, directory)))
                            ImportShortcutDependency(root, source, record, Path.Combine(source, directory));
                    string replaced = ReplacePathArguments(arguments, Path.Combine(source, ExeName), Path.Combine(root, ExeName));
                    replaced = ReplacePathArguments(replaced, source, ImportPath(root, record, ""));
                    if (replaced != arguments)
                    {
                        shortcut.GetType().InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { replaced });
                        changed = true;
                    }
                }
                if (changed) shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
                if (shell != null) Marshal.FinalReleaseComObject(shell);
            }
        }

        private static void ImportShortcutDependency(string root, string source, LocalUpgradeRecord record, string dependency)
        {
            if (SamePath(dependency, source) || SamePath(dependency, Path.Combine(source, ExeName))) return;
            bool directory = Directory.Exists(dependency);
            foreach (LocalUpgradeCleanupFile file in record.CleanupFiles)
            {
                string original = Path.Combine(source, file.Path);
                if (file.ImportedPath == null && !IsExe(file.Path) && (SamePath(original, dependency) || (directory && IsWithin(original, dependency))))
                    CopyImportedFile(root, source, record, file, file.Path, file.Hash);
            }
            if (directory)
            {
                EnsureImportDirectory(root, record, dependency.Substring(source.Length + 1));
                foreach (string child in record.CleanupDirectories)
                    if (IsWithin(Path.Combine(source, child), dependency)) EnsureImportDirectory(root, record, child);
            }
        }

        private static bool ContainsArgumentPath(string arguments, string path)
        {
            int start = 0;
            while ((start = arguments.IndexOf(path, start, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int end = start + path.Length;
                if (end == arguments.Length || "\"' \t".IndexOf(arguments[end]) >= 0) return true;
                start = end;
            }
            return false;
        }

        private static bool ShortcutReferences(string path, string source)
        {
            object shell = null, shortcut = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
                shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                foreach (string property in new[] { "TargetPath", "WorkingDirectory" })
                {
                    string value = shortcut.GetType().InvokeMember(property, BindingFlags.GetProperty, null, shortcut, null) as string;
                    if (!string.IsNullOrEmpty(value) && Path.IsPathRooted(value) && (SamePath(value, source) || IsWithin(value, source))) return true;
                }
                string arguments = shortcut.GetType().InvokeMember("Arguments", BindingFlags.GetProperty, null, shortcut, null) as string;
                return !string.IsNullOrEmpty(arguments) && arguments.IndexOf(source, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            finally
            {
                if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
                if (shell != null) Marshal.FinalReleaseComObject(shell);
            }
        }

        private static string ReplacePathArguments(string text, string source, string destination)
        {
            int start = 0;
            while ((start = text.IndexOf(source, start, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int end = start + source.Length;
                if (end == text.Length || "\\/\"' \t".IndexOf(text[end]) >= 0)
                { text = text.Substring(0, start) + destination + text.Substring(end); start += destination.Length; }
                else start = end;
            }
            return text;
        }

        private static bool IsProgramFile(string name)
        {
            foreach (string known in ProgramFiles)
                if (string.Equals(name, known, StringComparison.OrdinalIgnoreCase)) return true;
            return name.StartsWith(SettingsName + ".", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(ExeName + ".", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWithin(string path, string directory)
        {
            return Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPersonalDirectory(string path)
        {
            if (Path.GetDirectoryName(path) == null) return true;
            foreach (Environment.SpecialFolder folder in new[] { Environment.SpecialFolder.UserProfile,
                Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.CommonDesktopDirectory,
                Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.Windows, Environment.SpecialFolder.System })
            {
                string known = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(known) && SamePath(path, known)) return true;
            }
            return SamePath(path, Path.GetTempPath());
        }

        private static void ValidateCleanupPlan(string source, LocalUpgradeRecord record)
        {
            if (record.CleanupFiles == null || record.CleanupDirectories == null || record.CleanupFiles.Count > 20000)
                throw new InvalidDataException("旧版清理记录不完整。");
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (LocalUpgradeCleanupFile file in record.CleanupFiles)
            {
                ValidateCleanupPath(source, file.Path);
                if (!seen.Add(file.Path) || file.Hash == null || file.Hash.Length != 64)
                    throw new InvalidDataException("旧版清理校验信息不正确。");
                foreach (char c in file.Hash) if (!Uri.IsHexDigit(c)) throw new InvalidDataException("旧版清理校验信息不正确。");
                if (file.ImportedPath != null)
                {
                    ValidateRelativePath(file.ImportedPath);
                    if (string.IsNullOrEmpty(file.ImportedHash) || file.ImportedHash.Length != 64)
                        throw new InvalidDataException("迁移文件校验信息不正确。");
                }
            }
            foreach (string directory in record.CleanupDirectories) ValidateCleanupPath(source, directory);
        }

        private static void ValidateCleanupPath(string source, string relative)
        {
            ValidateRelativePath(relative);
            if (!IsWithin(Path.Combine(source, relative), source)) throw new InvalidDataException("旧版清理路径超出范围。");
        }

        private static void ValidateRelativePath(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.IndexOf('/') >= 0 ||
                relative.IndexOf(':') >= 0) throw new InvalidDataException("旧版清理路径不正确。");
            foreach (string part in relative.Split('\\'))
                if (part == ".." || part == "." || part.Length == 0) throw new InvalidDataException("旧版清理路径不正确。");
        }

        private static bool ReferencesPath(byte[] bytes, string path, string source)
        {
            AppSettings settings;
            try { settings = ReadSettings(bytes); } catch { return false; }
            if (settings == null) return false;
            foreach (DesktopItem item in settings.Items)
                if (SamePath(item.Path, path) || (!SamePath(item.Path, source) && Directory.Exists(item.Path) && IsWithin(path, item.Path))) return true;
            return false;
        }

        private static void DeleteEmptyDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                SafeDirectory(path);
                if (Directory.GetFileSystemEntries(path).Length == 0) Directory.Delete(path, false);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public static void RecordSuccessfulInstall(string currentDirectory)
        { RecordSuccessfulInstall(currentDirectory, RegistrationPath()); }

        internal static void RecordSuccessfulInstall(string currentDirectory, string registrationPath)
        {
            try
            {
                string root = SafeDirectory(currentDirectory);
                ReadVersion(Path.Combine(root, ExeName));
                string parent = Path.GetDirectoryName(Path.GetFullPath(registrationPath));
                Directory.CreateDirectory(parent);
                SafeDirectory(parent);
                WriteXml(registrationPath, new LocalInstallation { Directory = root });
            }
            catch { }
        }

        internal static List<LocalUpgradeCandidate> FindCandidates(string registrationPath)
        {
            List<LocalUpgradeCandidate> candidates = RunningCopies();
            try
            {
                LocalInstallation installed = ReadXml<LocalInstallation>(registrationPath);
                candidates.Add(new LocalUpgradeCandidate(installed.Directory, false, 2));
            }
            catch { }
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    string command = key == null ? null : key.GetValue("DeskDecor") as string;
                    string path = ParseStartupPath(command);
                    if (path != null) candidates.Add(new LocalUpgradeCandidate(Path.GetDirectoryName(path), false, 1));
                }
            }
            catch { }
            return candidates;
        }

        internal static string ParseStartupPath(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            command = command.Trim();
            string value;
            if (command[0] == '"')
            {
                int end = command.IndexOf('"', 1);
                if (end < 0) return null;
                value = command.Substring(1, end - 1);
            }
            else
            {
                int end = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (end < 0) return null;
                value = command.Substring(0, end + 4);
            }
            return Path.IsPathRooted(value) && string.Equals(Path.GetFileName(value), ExeName, StringComparison.OrdinalIgnoreCase)
                ? value : null;
        }

        private static List<LocalUpgradeCandidate> RunningCopies()
        {
            List<LocalUpgradeCandidate> copies = new List<LocalUpgradeCandidate>();
            using (Process current = Process.GetCurrentProcess())
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                foreach (Process process in Process.GetProcessesByName("DeskDecor"))
                using (process)
                {
                    IntPtr token = IntPtr.Zero;
                    try
                    {
                        if (process.Id == current.Id || process.SessionId != current.SessionId ||
                            !OpenProcessToken(process.Handle, 8, out token)) continue;
                        using (WindowsIdentity owner = new WindowsIdentity(token))
                            if (!object.Equals(identity.User, owner.User)) continue;
                        string exe = process.MainModule.FileName;
                        if (string.Equals(Path.GetFileName(exe), ExeName, StringComparison.OrdinalIgnoreCase))
                            copies.Add(new LocalUpgradeCandidate(Path.GetDirectoryName(exe), true, 3));
                    }
                    catch { }
                    finally { if (token != IntPtr.Zero) CloseHandle(token); }
                }
            }
            return copies;
        }

        private static bool IsRunning(string directory)
        {
            foreach (LocalUpgradeCandidate copy in RunningCopies())
                if (SamePath(copy.Directory, directory)) return true;
            return false;
        }

        private static string RegistrationPath()
        { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskDecor", "installation.xml"); }

        private static Version ReadVersion(string executable)
        {
            RejectReparse(executable);
            AssemblyName assembly = AssemblyName.GetAssemblyName(executable);
            if (!string.Equals(assembly.Name, "DeskDecor", StringComparison.Ordinal))
                throw new InvalidDataException("不是 DeskDecor 程序。");
            return assembly.Version;
        }

        private static string SafeDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidDataException("目录为空。");
            string full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(full)) throw new DirectoryNotFoundException(full);
            for (DirectoryInfo info = new DirectoryInfo(full); info != null; info = info.Parent)
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("不迁移链接目录。");
            return full;
        }

        private static string SafeUpdates(string root)
        {
            string path = Path.Combine(root, ".migration");
            Directory.CreateDirectory(path);
            return SafeDirectory(path);
        }

        private static bool IsSourceTree(string directory)
        {
            if (File.Exists(Path.Combine(directory, "build.ps1")) || Directory.GetFiles(directory, "*.cs").Length != 0) return true;
            for (DirectoryInfo info = new DirectoryInfo(directory); info != null; info = info.Parent)
                if (Directory.Exists(Path.Combine(info.FullName, ".git")) || File.Exists(Path.Combine(info.FullName, ".git"))) return true;
            return false;
        }

        private static bool SamePath(string left, string right)
        { return string.Equals(Path.GetFullPath(left).TrimEnd('\\'), Path.GetFullPath(right).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }

        private static void RejectReparse(string path)
        {
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("不迁移链接文件。");
        }

        private static byte[] ReadOptional(string path)
        {
            if (!File.Exists(path)) return null;
            RejectReparse(path);
            using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length > MaximumSettingsBytes) throw new InvalidDataException("配置文件过大。");
                using (MemoryStream output = new MemoryStream()) { input.CopyTo(output); return output.ToArray(); }
            }
        }

        private static AppSettings ReadSettings(byte[] bytes)
        {
            if (bytes == null) return null;
            using (MemoryStream input = new MemoryStream(bytes))
            using (XmlReader reader = XmlReader.Create(input, SafeXml()))
            {
                AppSettings settings = (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(reader);
                settings.Normalize();
                return settings;
            }
        }
        private static bool IsValidSettings(byte[] bytes)
        { try { return ReadSettings(bytes) != null; } catch { return false; } }
        private static string HashBytes(byte[] bytes)
        {
            if (bytes == null) return string.Empty;
            using (MemoryStream input = new MemoryStream(bytes)) return HashStream(input);
        }
        private static string HashFile(string path)
        {
            RejectReparse(path);
            using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) return HashStream(input);
        }
        private static string HashStream(Stream input)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }
        private static void WriteNew(string path, byte[] bytes)
        {
            using (FileStream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
        }
        private static XmlReaderSettings SafeXml()
        { return new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumSettingsBytes }; }
        private static T ReadXml<T>(string path)
        {
            RejectReparse(path);
            using (XmlReader reader = XmlReader.Create(path, SafeXml())) return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
        }
        private static void WriteXml<T>(string path, T value)
        {
            RejectReparse(path);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { new XmlSerializer(typeof(T)).Serialize(output, value); output.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);
    }

    public sealed class LocalInstallation { public string Directory; }
}
