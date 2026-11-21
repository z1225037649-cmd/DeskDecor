using System;
using System.Collections.Generic;
using System.Drawing;
using DeskDecor;

internal static class ReorderRegression
{
    private static readonly Rectangle Group = new Rectangle(0, 0, 364, 220);
    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    private static List<DesktopItem> Items()
    {
        var items = new List<DesktopItem>();
        for (int i = 0; i < 4; i++) items.Add(new DesktopItem(((char)('A' + i)).ToString(), 18 + i * 82, 18));
        return items;
    }
    private static string Order(List<DesktopItem> items)
    { return string.Join("", items.ConvertAll(item => item.Path).ToArray()); }
    private static void Move(ItemReorderGesture gesture, List<DesktopItem> visible, int x, int y)
    { gesture.Update(new Point(x, y), visible, Group, 4, new Size(82, 82), new Size(76, 76)); }

    public static void Main()
    {
        var items = Items();
        var visible = new List<DesktopItem>(items);
        var gesture = new ItemReorderGesture();
        gesture.Begin(items[0], new Point(54, 54), new Size(8, 8));
        Move(gesture, visible, 56, 55);
        Expect(!gesture.Dragging && !gesture.Commit(items, visible), "Click jitter must not start or save a reorder");

        gesture.Begin(items[0], new Point(54, 54), new Size(8, 8));
        Move(gesture, visible, 120, 54);
        Move(gesture, visible, 210, 54);
        Move(gesture, visible, 320, 54);
        Expect(gesture.Dragging && Order(items) == "ABCD", "Crossing multiple icons must keep the original order until release");
        // The final release goes back to C's left half, not D's earlier slot.
        Move(gesture, visible, 200, 54);
        Expect(gesture.Commit(items, visible) && Order(items) == "BACD", "Release must insert at the final slot, shifting intermediate icons");
        Expect(!gesture.Commit(items, visible), "Release must commit at most once");
        Console.WriteLine("PASS: threshold, crossing without changes, final-release insertion and single commit");

        items = Items(); visible = new List<DesktopItem>(items);
        gesture.Begin(items[3], new Point(300, 54), new Size(8, 8));
        Move(gesture, visible, 200, 54);
        Move(gesture, visible, 30, 54);
        gesture.Cancel();
        Expect(!gesture.Commit(items, visible) && Order(items) == "ABCD", "Escape/capture loss must discard the preview");

        gesture.Begin(items[0], new Point(54, 54), new Size(8, 8));
        Move(gesture, visible, 320, 54);
        Move(gesture, visible, 600, 54);
        Expect(gesture.DropIndex == -1 && !gesture.Commit(items, visible) && Order(items) == "ABCD",
            "Dropping outside the group must cancel, even after crossing a valid target");
        gesture.Begin(items[1], new Point(136, 54), new Size(8, 8));
        Move(gesture, visible, 152, 54);
        Expect(!gesture.Commit(items, visible) && Order(items) == "ABCD", "Dropping back on the original slot must be a no-op");
        Console.WriteLine("PASS: cancellation, outside release and original-position no-op");

        var metadata = new DesktopItem("desktop.ini", 0, 0);
        items.Insert(1, metadata);
        gesture.Begin(visible[0], new Point(54, 54), new Size(8, 8));
        Move(gesture, visible, 320, 160);
        Expect(gesture.DropIndex == 4 && gesture.Commit(items, visible), "Empty trailing space must accept an append");
        Expect(items.Contains(metadata) && items.Count == 5 && items[items.Count - 1].Path == "A",
            "Reorder must retain every item, including hidden metadata");
        Console.WriteLine("PASS: trailing-space append retains all saved entries");

        var a = new DesktopItem("A", 18, 18) { GroupId = "source" };
        var b = new DesktopItem("B", 100, 18) { GroupId = "source" };
        var c = new DesktopItem("C", 182, 18) { GroupId = "source" };
        var otherOne = new DesktopItem("X", 600, 18) { GroupId = "other" };
        var otherTwo = new DesktopItem("Y", 682, 18) { GroupId = "other" };
        metadata = new DesktopItem("desktop.ini", 0, 0) { GroupId = "source" };
        items = new List<DesktopItem> { a, otherOne, b, metadata, otherTwo, c };
        visible = new List<DesktopItem> { a, b, c };
        gesture.Begin(a, new Point(54, 54), new Size(8, 8));
        Move(gesture, visible, 320, 160);
        Expect(Order(items) == "AXBdesktop.iniYC", "A multi-group preview must not reorder the global collection");
        Expect(gesture.Commit(items, visible), "Appending within one group must commit");
        Expect(items.Count == 6 && items.Contains(metadata) &&
            items.IndexOf(otherOne) < items.IndexOf(otherTwo) &&
            items.IndexOf(b) < items.IndexOf(c) && items.IndexOf(c) < items.IndexOf(a),
            "A group reorder must retain metadata, all entries and the other group's relative order");
        Expect(a.GroupId == "source" && b.GroupId == "source" && c.GroupId == "source" &&
            otherOne.GroupId == "other" && otherTwo.GroupId == "other" && otherOne.X == 600 && otherTwo.X == 682,
            "Sorting one group must not reassign entries or move another group's tiles");
        string beforeOutside = Order(items);
        visible = new List<DesktopItem> { b, c, a };
        gesture.Begin(b, new Point(136, 54), new Size(8, 8));
        Move(gesture, visible, 620, 54);
        Expect(!gesture.Commit(items, visible) && Order(items) == beforeOutside,
            "Releasing over another group must cancel rather than move or reorder entries");
        Console.WriteLine("PASS: multi-group reorder retains other-group order, metadata and assignments; cross-group release cancels");
    }
}
