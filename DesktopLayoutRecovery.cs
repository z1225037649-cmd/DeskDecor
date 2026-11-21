using System;
using System.Collections.Generic;
using System.Drawing;

namespace DeskDecor
{
    internal static class DesktopLayoutRecovery
    {
        private const int MinimumWidth = 160;
        private const int MinimumHeight = 80;

        public static bool Restore(GroupLayout group, IList<DesktopItem> items, IList<Rectangle> workingAreas)
        {
            if (group == null) throw new ArgumentNullException("group");
            if (workingAreas == null) throw new ArgumentNullException("workingAreas");
            if (workingAreas.Count == 0) return false;

            int savedWidth = Math.Max(MinimumWidth, group.Width);
            int savedHeight = Math.Max(MinimumHeight, group.Height);
            int headerHeight = MonitorDpi.Scale(34, group.Dpi);
            Rectangle original = new Rectangle(group.X, group.Y,
                savedWidth, group.Collapsed ? headerHeight : savedHeight);
            Rectangle target = SelectWorkingArea(original, workingAreas, headerHeight);

            int width = Math.Min(savedWidth, Math.Max(MinimumWidth, target.Width));
            int height = group.Collapsed ? savedHeight : Math.Min(savedHeight, Math.Max(MinimumHeight, target.Height));
            if (!group.Collapsed)
            {
                int count = 0;
                if (items != null)
                    foreach (DesktopItem item in items)
                        if (item != null) count++;
                FitExpandedContent(group.Dpi, count, target, ref width, ref height);
            }
            int x = Clamp(group.X, target.Left, target.Right - width);
            int y = Clamp(group.Y, target.Top, target.Bottom - Math.Min(headerHeight, height));
            if (!group.Collapsed)
                y = Clamp(y, target.Top, Math.Max(target.Top, target.Bottom - height));

            int dx = x - group.X;
            int dy = y - group.Y;
            bool changed = dx != 0 || dy != 0 || width != group.Width || height != group.Height;
            if (!changed) return false;

            group.X = x;
            group.Y = y;
            group.Width = width;
            group.Height = height;
            if (items != null)
                foreach (DesktopItem item in items)
                {
                    if (item == null) continue;
                    item.X += dx;
                    item.Y += dy;
                }
            return true;
        }

        private static void FitExpandedContent(int dpi, int count, Rectangle area, ref int width, ref int height)
        {
            int padding = MonitorDpi.Scale(18, dpi);
            int header = MonitorDpi.Scale(34, dpi);
            int tileWidth = MonitorDpi.Scale(76, dpi), tileHeight = MonitorDpi.Scale(66, dpi);
            int columnPitch = MonitorDpi.Scale(82, dpi), rowPitch = MonitorDpi.Scale(76, dpi);
            int availableRows = Math.Max(1, 1 + (area.Height - header - padding * 2 - tileHeight) / rowPitch);
            int availableColumns = Math.Max(1, (area.Width - padding * 2 + columnPitch - tileWidth) / columnPitch);
            int currentColumns = Math.Max(1, (width - padding * 2 + columnPitch - tileWidth) / columnPitch);
            int neededColumns = Math.Max(1, (count + availableRows - 1) / availableRows);
            int columns = Math.Min(availableColumns, Math.Max(currentColumns, neededColumns));
            // Reflow narrow, tall groups horizontally before the overlay lays out
            // entries. Otherwise layout undoes the height clamp every timer tick.
            width = Math.Max(width, padding * 2 + tileWidth + (columns - 1) * columnPitch);
            columns = Math.Max(1, (width - padding * 2 + columnPitch - tileWidth) / columnPitch);
            int rows = (count + columns - 1) / columns;
            // If the content exceeds the monitor's total capacity, preserve its
            // required height so recovery still converges rather than oscillates.
            height = Math.Max(height, header + padding * 2 + Math.Max(0, rows - 1) * rowPitch + tileHeight);
        }

        private static Rectangle SelectWorkingArea(Rectangle group, IList<Rectangle> areas, int headerHeight)
        {
            int bestArea = 0;
            Rectangle best = areas[0];
            foreach (Rectangle area in areas)
            {
                Rectangle overlap = Rectangle.Intersect(group, area);
                int areaSize = overlap.IsEmpty ? 0 : overlap.Width * overlap.Height;
                if (areaSize > bestArea)
                {
                    bestArea = areaSize;
                    best = area;
                }
            }
            if (bestArea > 0) return best;

            long bestDistance = long.MaxValue;
            foreach (Rectangle area in areas)
            {
                long dx = DistanceToInterval(group.Left + group.Width / 2, area.Left, area.Right);
                long dy = DistanceToInterval(group.Top + headerHeight / 2, area.Top, area.Bottom);
                long distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = area;
                }
            }
            return best;
        }

        private static long DistanceToInterval(int value, int low, int high)
        {
            if (value < low) return (long)low - value;
            if (value > high) return (long)value - high;
            return 0;
        }

        private static int Clamp(int value, int low, int high)
        {
            if (high < low) return low;
            return Math.Max(low, Math.Min(high, value));
        }
    }
}
