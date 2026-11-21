using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using DeskDecor;

internal static class OverlayLayoutRegression
{
    private static readonly MethodInfo Layout = typeof(OverlayForm).GetMethod("LayoutItemsInGroup",
        BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(GroupLayout) }, null);

    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    [STAThread] public static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try
        {
            RunCase(12, 120, new Rectangle(0, 0, 1920, 1032), true);
            RunCase(12, 120, new Rectangle(0, 0, 800, 560), true);
            RunCase(12, 144, new Rectangle(1920, 40, 1280, 984), true);
            RunCase(100, 120, new Rectangle(0, 0, 400, 300), false);
            RunInactiveGroup();
            Console.WriteLine("PASS: real overlay layout fits recovered content and stops repeated recovery, including capacity overflow (no visible test windows)");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            Environment.ExitCode = 1;
        }
    }

    private static void RunInactiveGroup()
    {
        var settings = new AppSettings { DesktopInitialized = true };
        settings.Groups.Add(settings.Group);
        settings.ActiveGroupId = settings.Group.Id;
        var other = new GroupLayout { X = 100, Y = 0, Width = 208, Height = 1213, Dpi = 120 };
        settings.Groups.Add(other);
        for (int i = 0; i < 12; i++)
            settings.Items.Add(new DesktopItem("C:\\inactive-group-test-" + i + ".txt", 0, 0) { GroupId = other.Id });
        using (var overlay = new OverlayForm(settings))
        {
            foreach (var item in settings.Items)
                Expect(new Rectangle(other.X, other.Y, other.Width, other.Height).Contains(item.X, item.Y),
                    "Startup must reflow recovered entries in inactive groups as well as the active group");
            Expect(settings.ActiveGroupId == settings.Group.Id && !overlay.IsHandleCreated,
                "Reflow must preserve the selected group and never create a test window");
        }
    }

    private static void RunCase(int count, int dpi, Rectangle area, bool allContentFits)
    {
        var settings = new AppSettings { DesktopInitialized = true };
        var group = settings.Group;
        settings.Groups.Add(group);
        settings.ActiveGroupId = group.Id;
        for (int i = 0; i < count; i++)
            settings.Items.Add(new DesktopItem("C:\\overlay-layout-test-" + i + ".txt", 0, 0) { GroupId = group.Id });
        using (var overlay = new OverlayForm(settings))
        {
            Expect(!overlay.Visible && !overlay.IsHandleCreated, "Layout tests must not create or show a desktop surface");
            group.X = area.Left + 12; group.Y = area.Top;
            group.Width = MonitorDpi.Scale(166, dpi); group.Height = MonitorDpi.Scale(970, dpi); group.Dpi = dpi;
            var areas = new List<Rectangle> { area };
            Expect(DesktopLayoutRecovery.Restore(group, settings.Items, areas), "Oversized layout must be recovered");
            Layout.Invoke(overlay, new object[] { group });
            for (int tick = 0; tick < 3; tick++)
                Expect(!DesktopLayoutRecovery.Restore(group, settings.Items, areas),
                    "Recovery and actual overlay layout must converge instead of rewriting settings every timer tick");
            if (allContentFits)
            {
                Expect(area.Contains(new Rectangle(group.X, group.Y, group.Width, group.Height)),
                    "Recovered group must fit the monitor working area");
                foreach (var item in settings.Items)
                    Expect(area.Contains(new Rectangle(item.X, item.Y, MonitorDpi.Scale(76, dpi), MonitorDpi.Scale(66, dpi))),
                        "Recovered entries must remain on screen after reflow");
            }
            Expect(!overlay.IsHandleCreated, "Regression checks must stay independent of desktop window attachment");
        }
    }
}
