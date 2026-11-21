using System.Collections.Generic;
using System.Drawing;
using DeskDecor;

internal static class DesktopLayoutRecoveryRegression
{
    private static void Expect(bool condition, string message)
    { if (!condition) throw new System.Exception(message); }

    public static void Main()
    {
        try { Run(); }
        catch (System.Exception ex)
        {
            System.Console.Error.WriteLine("FAIL: " + ex.Message);
            System.Environment.ExitCode = 1;
        }
    }

    private static void Run()
    {
        var areas = new List<Rectangle> { new Rectangle(0, 0, 1920, 1080), new Rectangle(1920, 0, 2560, 1440) };
        var group = new GroupLayout { X = -1700, Y = 1280, Width = 620, Height = 400, Dpi = 96 };
        var items = new List<DesktopItem>
        {
            new DesktopItem("C:\\one.txt", -1660, 1360),
            new DesktopItem("C:\\two.txt", -1580, 1360)
        };
        var relative = new Point(items[0].X - group.X, items[0].Y - group.Y);
        Expect(DesktopLayoutRecovery.Restore(group, items, areas), "An off-screen group must be moved back onto a connected monitor");
        Expect(group.X >= 0 && group.Y >= 0 && group.X + group.Width <= 1920,
            "The restored group must fit the nearest monitor's working area");
        Expect(items[0].X - group.X == relative.X && items[0].Y - group.Y == relative.Y,
            "Recovery must move all group entries with their original relative positions");
        Expect(!DesktopLayoutRecovery.Restore(group, items, areas), "A visible group must not be moved repeatedly");

        group = new GroupLayout { X = 4200, Y = 400, Width = 500, Height = 320, Collapsed = true };
        items.Clear();
        items.Add(new DesktopItem("C:\\one.txt", 4260, 460));
        Expect(DesktopLayoutRecovery.Restore(group, items, areas), "A collapsed group on a disconnected monitor must be recovered");
        Expect(group.X + 72 <= 4480 && group.Y < 1440 && group.Height == 320,
            "Collapsed recovery must keep the title reachable and preserve expanded height");
        Expect(!DesktopLayoutRecovery.Restore(new GroupLayout(), items, new List<Rectangle>()),
            "Recovery must safely skip when Windows reports no working areas");

        group = new GroupLayout { X = 100, Y = 60, Width = 320, Height = 220, Dpi = 96 };
        items = new List<DesktopItem> { new DesktopItem("C:\\dpi.txt", 140, 110) };
        Expect(MonitorDpi.UpdateGroupDpi(group, items, 144, 240, 120), "Changing to a 150% monitor must rescale the group");
        Expect(group.Width == 480 && group.Height == 330 && items[0].X == 160 && items[0].Y == 135 && group.Dpi == 144,
            "DPI changes must scale size and entry positions around the group origin");
        Expect(!MonitorDpi.UpdateGroupDpi(group, items, 144, 240, 120), "Unchanged monitor DPI must not rescale repeatedly");
        Expect(MonitorDpi.UpdateGroupDpi(group, items, 96, 150, 100) && group.Width == 320 && items[0].X == 140,
            "Returning to 100% DPI must restore the original logical geometry");
        System.Console.WriteLine("PASS: off-screen recovery, nearest-monitor selection, relative entry positions, collapsed geometry and DPI scaling");
    }
}
