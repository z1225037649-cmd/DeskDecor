using System;
using System.Collections.Generic;
using System.Drawing;

namespace DeskDecor
{
    // Mouse movement only updates a preview. The item collection is changed
    // once, by Commit, when the user releases the left button on a valid slot.
    internal sealed class ItemReorderGesture
    {
        public DesktopItem Source { get; private set; }
        public bool Dragging { get; private set; }
        public Point PressPoint { get; private set; }
        public Point Pointer { get; private set; }
        public int DropIndex { get; private set; }
        private Rectangle _clickTolerance;

        public void Begin(DesktopItem source, Point point, Size dragSize)
        {
            Cancel();
            Source = source;
            PressPoint = Pointer = point;
            _clickTolerance = new Rectangle(point.X - dragSize.Width / 2,
                point.Y - dragSize.Height / 2, Math.Max(1, dragSize.Width), Math.Max(1, dragSize.Height));
        }

        public void Update(Point point, IList<DesktopItem> visible, Rectangle group,
            int columns, Size cell, Size tile)
        {
            if (Source == null) return;
            Pointer = point;
            if (!Dragging && !_clickTolerance.Contains(point)) Dragging = true;
            DropIndex = -1;
            if (!Dragging || !group.Contains(point) || visible.Count == 0) return;
            columns = Math.Max(1, columns);
            DesktopItem first = visible[0];
            int row = Math.Max(0, (point.Y - first.Y + (cell.Height - tile.Height) / 2) / cell.Height);
            int column = Math.Max(0, Math.Min(columns - 1,
                (point.X - first.X + (cell.Width - tile.Width) / 2) / cell.Width));
            int index = row * columns + column;
            if (index >= visible.Count) { DropIndex = visible.Count; return; }
            DropIndex = index + (point.X >= visible[index].X + tile.Width / 2 ? 1 : 0);
        }

        public bool Commit(List<DesktopItem> items, IList<DesktopItem> visible)
        {
            DesktopItem source = Source;
            int slot = DropIndex;
            bool dragging = Dragging;
            Cancel();
            if (!dragging || source == null || slot < 0 || slot > visible.Count) return false;
            int from = visible.IndexOf(source);
            if (from < 0 || !items.Contains(source) || slot == from || slot == from + 1) return false;
            DesktopItem before = slot < visible.Count ? visible[slot] : null;
            if (before != null && !items.Contains(before)) return false;
            items.Remove(source);
            if (before == null) items.Add(source);
            else items.Insert(items.IndexOf(before), source);
            return true;
        }

        public void Cancel()
        {
            Source = null;
            Dragging = false;
            DropIndex = -1;
        }
    }
}
