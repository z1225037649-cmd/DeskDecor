using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace DeskDecor
{
    [Serializable]
    public sealed class DesktopItem : EventArgs
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public DesktopItem() { }

        public DesktopItem(string path, int x, int y)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(Name)) Name = path;
            X = x;
            Y = y;
        }
    }

    [Serializable]
    public sealed class GroupLayout
    {
        public string Title { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Collapsed { get; set; }

        public string DisplayTitle { get { return string.IsNullOrWhiteSpace(Title) ? "快捷入口" : Title; } }
    }

    [Serializable]
    public sealed class AppSettings
    {
        public List<DesktopItem> Items { get; set; }
        public GroupLayout Group { get; set; }
        public bool TaskbarTransparent { get; set; }
        public bool DesktopIconsHidden { get; set; }
        public bool DesktopInitialized { get; set; }

        public AppSettings()
        {
            Items = new List<DesktopItem>();
            Group = new GroupLayout();
            TaskbarTransparent = false;
        }

        public void Normalize()
        {
            if (Items == null) Items = new List<DesktopItem>();
            if (Group == null) Group = new GroupLayout();
            foreach (DesktopItem item in Items)
                if (item == null || string.IsNullOrWhiteSpace(item.Path))
                    throw new InvalidDataException("配置中包含无效的桌面项目。");
        }
    }

    internal static class SettingsStore
    {
        public static AppSettings Load(string path)
        {
            string warning;
            bool recovered;
            return Load(path, out warning, out recovered);
        }

        public static AppSettings Load(string path, out string warning, out bool recoveredFromBackup)
        {
            warning = null;
            recoveredFromBackup = false;
            if (!File.Exists(path))
            {
                string missingBackup = path + ".bak";
                if (!File.Exists(missingBackup)) return new AppSettings();
                try
                {
                    AppSettings recovered = ReadValidated(missingBackup);
                    recoveredFromBackup = true;
                    warning = "配置文件缺失，已从上次有效备份恢复。";
                    try { RestorePrimary(path, missingBackup); }
                    catch (Exception ex) { warning += " 恢复内容已载入，但未能修复主文件：" + ex.Message; }
                    return recovered;
                }
                catch (Exception ex)
                {
                    warning = "配置文件缺失，备份也无法读取。备份已保留；本次使用空白配置。" + ex.Message;
                    return new AppSettings();
                }
            }

            Exception primaryError;
            try { return ReadValidated(path); }
            catch (Exception ex) { primaryError = ex; }

            string backup = path + ".bak";
            if (File.Exists(backup))
            {
                try
                {
                    AppSettings restored = ReadValidated(backup);
                    recoveredFromBackup = true;
                    warning = "配置文件无法读取，已从上次有效备份恢复。";
                    try { RestorePrimary(path, backup); }
                    catch (Exception ex)
                    {
                        warning += " 恢复内容已载入，但未能修复主文件：" + ex.Message;
                    }
                    return restored;
                }
                catch (Exception backupError)
                {
                    warning = "配置文件和备份均无法读取。原文件已保留；本次使用空白配置。主文件：" +
                        primaryError.Message + "；备份：" + backupError.Message;
                    return new AppSettings();
                }
            }

            warning = "配置文件无法读取，原文件已保留；本次使用空白配置。" + primaryError.Message;
            return new AppSettings();
        }

        public static void Save(string path, AppSettings settings)
        {
            Save(path, settings, false);
        }

        public static void Save(string path, AppSettings settings, bool preserveBackup)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            string temp = path + ".tmp";
            string backup = path + ".bak";
            try
            {
                using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    new XmlSerializer(typeof(AppSettings)).Serialize(stream, settings);
                    stream.Flush(true);
                }

                // Verify the complete serialized document before touching the last
                // known-good copy. Replace is a same-volume atomic operation.
                ReadValidated(temp);
                if (!File.Exists(path))
                    File.Move(temp, path);
                else
                    File.Replace(temp, path, preserveBackup ? null : backup, true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private static AppSettings ReadValidated(string path)
        {
            AppSettings settings;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                settings = (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(stream);
            if (settings == null) throw new InvalidDataException("配置为空。");
            settings.Normalize();
            return settings;
        }

        private static void RestorePrimary(string path, string backup)
        {
            string temp = path + ".recover.tmp";
            try
            {
                using (FileStream input = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                    output.Flush(true);
                }
                ReadValidated(temp);
                if (File.Exists(path)) File.Replace(temp, path, null, true);
                else File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }
}
