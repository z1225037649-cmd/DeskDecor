using System;
using System.Collections.Generic;
using System.IO;

namespace DeskDecor
{
    internal static class DesktopEntries
    {
        public static bool IsMetadata(string path)
        {
            string name = Path.GetFileName(path);
            return string.Equals(name, "desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Thumbs.db", StringComparison.OrdinalIgnoreCase);
        }

        private static string CanonicalPath(string path)
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetPathRoot(full);
            return full.Length > root.Length ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : full;
        }

        public static int AddPaths(AppSettings settings, IEnumerable<string> paths)
        {
            return AddPaths(settings, paths, string.IsNullOrWhiteSpace(settings.ActiveGroupId) ? settings.Group.Id : settings.ActiveGroupId);
        }

        public static int AddPaths(AppSettings settings, IEnumerable<string> paths, string groupId)
        {
            if (paths == null) return 0;
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DesktopItem item in settings.Items)
                try { known.Add(CanonicalPath(item.Path)); } catch { }
            int added = 0;
            foreach (string path in paths)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    string full = CanonicalPath(path);
                    if (IsMetadata(full) || (!File.Exists(full) && !Directory.Exists(full)) || !known.Add(full)) continue;
                    DesktopItem item = new DesktopItem(full, 0, 0);
                    item.GroupId = groupId;
                    settings.Items.Add(item);
                    added++;
                }
                catch (ArgumentException) { }
                catch (NotSupportedException) { }
                catch (IOException) { }
                catch (System.Security.SecurityException) { }
                catch (UnauthorizedAccessException) { }
            }
            return added;
        }

        public static bool Remove(AppSettings settings, DesktopItem item)
        {
            // Remove the saved reference only. Never delete the target on disk.
            return item != null && settings.Items.Remove(item);
        }
    }
}
