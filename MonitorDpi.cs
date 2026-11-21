using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DeskDecor
{
    internal static class MonitorDpi
    {
        public static int AtPoint(Point point)
        {
            try
            {
                IntPtr monitor = MonitorFromPoint(new NativePoint { X = point.X, Y = point.Y }, 2);
                uint x, y;
                return monitor != IntPtr.Zero && GetDpiForMonitor(monitor, 0, out x, out y) == 0 && x >= 48 && x <= 768
                    ? (int)x : 96;
            }
            catch (DllNotFoundException) { return 96; }
            catch (EntryPointNotFoundException) { return 96; }
        }

        public static int Scale(int value, int dpi)
        {
            return Math.Max(1, (int)Math.Round(value * Math.Max(48, Math.Min(768, dpi)) / 96.0));
        }

        public static bool UpdateGroupDpi(GroupLayout group, System.Collections.Generic.IList<DesktopItem> items,
            int dpi, int minimumWidth, int minimumHeight)
        {
            if (group == null) return false;
            dpi = Math.Max(48, Math.Min(768, dpi));
            int oldDpi = group.Dpi < 48 || group.Dpi > 768 ? 96 : group.Dpi;
            if (oldDpi == dpi) return false;
            double ratio = dpi / (double)oldDpi;
            group.Width = Math.Max(minimumWidth, (int)Math.Round(Math.Max(1, group.Width) * ratio));
            group.Height = Math.Max(minimumHeight, (int)Math.Round(Math.Max(1, group.Height) * ratio));
            if (items != null)
                foreach (DesktopItem item in items)
                    if (item != null)
                    {
                        item.X = group.X + (int)Math.Round((item.X - group.X) * ratio);
                        item.Y = group.Y + (int)Math.Round((item.Y - group.Y) * ratio);
                    }
            group.Dpi = dpi;
            return true;
        }

        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
        [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);
    }
}
