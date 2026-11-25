using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;
using DeskDecor;

internal static class RenderingInteractionRegression
{
    private static Bitmap Surface;
    private static Graphics Canvas;
    private static int Frames;
    private static bool Baseline;
    private static int Saves;
    private static int IconLoads;

    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private static object Call(object target, string name, params object[] args)
    { return target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args); }

    private static T Field<T>(object target, string name)
    { return (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target); }

    private static Bitmap Icon(string path)
    {
        IconLoads++;
        var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppPArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.OrangeRed);
        return bitmap;
    }

    // Replace only the HWND presentation boundary in the copied test source.
    // Actual redraw scheduling and PaintSurface still run for every frame.
    internal static bool Render(OverlayForm overlay)
    {
        if (Canvas != null)
        {
            Frames++;
            Canvas.Clear(Color.Transparent);
            overlay.PaintSurface(Canvas);
        }
        return true;
    }

    private static string Digest()
    {
        using (var stream = new MemoryStream())
        using (SHA256 hash = SHA256.Create())
        {
            Surface.Save(stream, ImageFormat.Png);
            return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
        }
    }

    private static void Report(string scenario, OverlayForm overlay, int maximumFrames)
    {
        Expect(Baseline || Frames <= maximumFrames, scenario + " must paint at most " + maximumFrames + " final frame(s)");
        Expect(!overlay.IsHandleCreated && !overlay.Visible, "Interaction tests must not create a desktop window");
        Console.WriteLine("FRAMES " + scenario + " " + Frames);
        Console.WriteLine("PIXELS " + scenario + " " + Digest());
        Frames = 0;
    }

    private static void Move(OverlayForm overlay, Point point, MouseButtons button)
    { Call(overlay, "OnMouseMove", new MouseEventArgs(button, 0, point.X, point.Y, 0)); }

    private static void Run()
    {
        var settings = new AppSettings { DesktopInitialized = true };
        settings.Groups.Add(settings.Group);
        var second = new GroupLayout { Title = "Other", X = 550, Y = 30, Width = 360, Height = 500 };
        settings.Groups.Add(second);
        settings.ActiveGroupId = settings.Group.Id;
        for (int i = 0; i < 6; i++)
            settings.Items.Add(new DesktopItem(@"C:\synthetic-interaction-" + i + ".txt", 0, 0)
                { GroupId = i < 3 ? settings.Group.Id : second.Id });
        using (var overlay = new OverlayForm(settings))
        using (Surface = new Bitmap(1200, 900, PixelFormat.Format32bppPArgb))
        using (Canvas = Graphics.FromImage(Surface))
        {
            ((ShellIconCache)Field<object>(overlay, "_icons")).Dispose();
            typeof(OverlayForm).GetField("_icons", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(overlay, new ShellIconCache(Icon));
            settings.Group.X = 30; settings.Group.Y = 30; settings.Group.Width = 360; settings.Group.Height = 500;
            settings.Group.Dpi = second.Dpi = 96;
            overlay.RefreshItemsLayout();
            overlay.ItemMoved += delegate { Saves++; };
            overlay.ActiveGroupChanged += delegate { Saves++; };
            DesktopItem target = settings.Items[3];
            Point hover = new Point(target.X + 5, target.Y + 5);
            Frames = 0;
            Move(overlay, hover, MouseButtons.None);
            Expect(settings.ActiveGroupId == second.Id && Saves == 1, "Hover must activate a new group exactly once");
            Report("cross-group-hover", overlay, 1);
            for (int i = 0; i < 200; i++) Move(overlay, new Point(hover.X + i % 2, hover.Y), MouseButtons.None);
            Report("steady-hover-200-events", overlay, 0);

            Call(overlay, "UpdateHover", new Point(int.MinValue, int.MinValue));
            Point press = new Point(second.X + 15, second.Y + 15);
            Call(overlay, "BeginGroupGesture", press, true, false);
            int oldX = second.X, oldItemX = target.X;
            Frames = 0;
            Move(overlay, new Point(press.X + 12, press.Y + 10), MouseButtons.Left);
            Expect(second.X == oldX + 12 && target.X == oldItemX + 12 && Saves == 1,
                "Group dragging must move items but defer saving until release");
            Report("group-drag-start", overlay, 1);
            Call(overlay, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, press.X + 12, press.Y + 10, 0));
            Expect(Saves == 2, "Group dragging must save once on release");

            Call(overlay, "UpdateHover", new Point(int.MinValue, int.MinValue));
            var gesture = Field<ItemReorderGesture>(overlay, "_reorder");
            DesktopItem source = settings.Items[3];
            DesktopItem last = settings.Items[5];
            Point sourcePoint = new Point(source.X + 5, source.Y + 5);
            Point drop = new Point(last.X + 70, last.Y + 15);
            gesture.Begin(source, sourcePoint, new Size(4, 4));
            Move(overlay, drop, MouseButtons.Left);
            Expect(gesture.Dragging && Saves == 2, "Item preview must not save before release");
            Frames = 0;
            Call(overlay, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, drop.X, drop.Y, 0));
            Expect(Saves == 3 && settings.Items[5] == source && !gesture.Dragging,
                "Item release must commit the final insertion once");
            Report("item-drop", overlay, 1);

            Move(overlay, new Point(second.X + 15, second.Y + 15), MouseButtons.None);
            Frames = 0;
            Call(overlay, "ToggleCollapsed");
            Expect(second.Collapsed && Saves == 4, "Collapse must save once");
            Report("collapse", overlay, 1);

            second.Collapsed = false;
            Call(overlay, "UpdateHover", new Point(int.MinValue, int.MinValue));
            Call(overlay, "BeginGroupGesture", new Point(second.X + second.Width, second.Y + second.Height), false, true);
            overlay.InvalidateSurface();
            string resizing = Digest();
            Frames = 0;
            Call(overlay, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
            Expect(Saves == 5 && !Field<bool>(overlay, "_resizingGroup"), "Resize release must end the gesture and save once");
            if (!Baseline)
            {
                Expect(Frames == 1 && resizing != Digest(), "Resize release must repaint the gesture highlight without depending on settings persistence");
                string released = Digest();
                Canvas.Clear(Color.Transparent);
                overlay.PaintSurface(Canvas);
                Expect(released == Digest(), "The rendered release frame must equal the final gesture state");
            }
            Expect(!overlay.IsHandleCreated, "Resize-release checks must not create a window");
        }
        Canvas = null; Surface = null;
        Console.WriteLine("PASS: real mouse events retain final pixels, group activation, drop order and save timing without visible windows");
    }

    private static void CheckOffscreenIcons()
    {
        var settings = new AppSettings { DesktopInitialized = true };
        settings.Groups.Add(settings.Group);
        settings.ActiveGroupId = settings.Group.Id;
        for (int i = 0; i < 3; i++)
            settings.Items.Add(new DesktopItem(@"C:\synthetic-clipping-" + i + ".txt", 0, 0) { GroupId = settings.Group.Id });
        using (var overlay = new OverlayForm(settings))
        using (Surface = new Bitmap(1200, 900, PixelFormat.Format32bppPArgb))
        using (Canvas = Graphics.FromImage(Surface))
        {
            Field<ShellIconCache>(overlay, "_icons").Dispose();
            typeof(OverlayForm).GetField("_icons", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(overlay, new ShellIconCache(Icon));
            settings.Group.Dpi = 96;
            settings.Items[0].X = 100; settings.Items[0].Y = 100;
            settings.Items[1].X = 1400; settings.Items[1].Y = 100;
            settings.Items[2].X = 100; settings.Items[2].Y = 1000;
            IconLoads = Frames = 0;
            overlay.InvalidateSurface();
            Expect(Baseline || IconLoads == 1, "Fully clipped entries must not load their icons");
            Console.WriteLine("ICON_LOADS initially-visible " + IconLoads);
            Report("clipped-entries", overlay, 1);
            settings.Items[1].X = 300;
            settings.Items[2].Y = 300;
            overlay.InvalidateSurface();
            Expect(IconLoads == 3, "Entries moving into view must load their icons and render immediately");
            Report("entries-enter-view", overlay, 1);
        }
        Canvas = null; Surface = null;
        Console.WriteLine("PASS: icon visibility cases render correctly and newly visible entries appear immediately");
    }

    [STAThread] private static int Main(string[] args)
    {
        Baseline = args.Length != 0 && args[0] == "baseline";
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try { Run(); CheckOffscreenIcons(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error.InnerException ?? error); return 1; }
    }
}
