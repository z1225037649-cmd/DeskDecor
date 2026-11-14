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
    }

    internal static class SettingsStore
    {
        public static AppSettings Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return new AppSettings();
                using (FileStream stream = File.OpenRead(path))
                    return (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(stream);
            }
            catch { return new AppSettings(); }
        }

        public static void Save(string path, AppSettings settings)
        {
            string temp = path + ".tmp";
            using (FileStream stream = File.Create(temp))
                new XmlSerializer(typeof(AppSettings)).Serialize(stream, settings);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
    }
}
