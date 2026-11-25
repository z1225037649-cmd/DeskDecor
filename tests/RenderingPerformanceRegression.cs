using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using DeskDecor;

internal static class RenderingPerformanceRegression
{
    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private static Bitmap Icon(string ignored)
    {
        var image = new Bitmap(32, 32, PixelFormat.Format32bppPArgb);
        using (Graphics graphics = Graphics.FromImage(image)) graphics.Clear(Color.CornflowerBlue);
        return image;
    }

    private static AppSettings Settings(int count)
    {
        var settings = new AppSettings { DesktopInitialized = true };
        settings.Group.Title = "常用软件 Example";
        settings.Group.Width = 1500;
        settings.Group.Height = 980;
        settings.Groups.Add(settings.Group);
        settings.ActiveGroupId = settings.Group.Id;
        for (int i = 0; i < count; i++)
            settings.Items.Add(new DesktopItem(@"C:\DeskDecor-synthetic-rendering\entry-" + i + ".txt", 0, 0)
                { GroupId = settings.Group.Id, Label = "测试入口 Entry " + i });
        return settings;
    }

    private static OverlayForm Overlay(AppSettings settings)
    {
        var overlay = new OverlayForm(settings);
        FieldInfo cache = typeof(OverlayForm).GetField("_icons", BindingFlags.Instance | BindingFlags.NonPublic);
        ((ShellIconCache)cache.GetValue(overlay)).Dispose();
        cache.SetValue(overlay, new ShellIconCache(Icon));
        return overlay;
    }

    private static void Layout(OverlayForm overlay, AppSettings settings, int dpi)
    {
        settings.Group.Dpi = dpi;
        settings.Group.X = 18;
        settings.Group.Y = 24;
        settings.Group.Width = 1450;
        settings.Group.Height = 980;
        overlay.RefreshItemsLayout();
    }

    private static string Pixels(OverlayForm overlay, Bitmap image, Graphics graphics)
    {
        graphics.Clear(Color.Transparent);
        overlay.PaintSurface(graphics);
        BitmapData data = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            byte[] pixels = new byte[data.Stride * image.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(pixels)).Replace("-", "");
        }
        finally { image.UnlockBits(data); }
    }

    private static void CheckPixels()
    {
        AppSettings settings = Settings(32);
        using (OverlayForm overlay = Overlay(settings))
        using (var image = new Bitmap(1800, 1400, PixelFormat.Format32bppPArgb))
        using (Graphics graphics = Graphics.FromImage(image))
        {
            foreach (int dpi in new[] { 96, 120, 144, 192 })
            foreach (int opacity in new[] { 100, 55 })
            {
                Layout(overlay, settings, dpi);
                settings.Opacity = opacity;
                string initial = Pixels(overlay, image, graphics);
                Expect(initial == Pixels(overlay, image, graphics), "Repeated paints must produce the same pixels");
                Console.WriteLine("PIXELS " + dpi + "-" + opacity + "-initial " + initial);
                settings.Group.X += 37;
                settings.Group.Y += 29;
                foreach (DesktopItem item in settings.Items) { item.X += 37; item.Y += 29; }
                Console.WriteLine("PIXELS " + dpi + "-" + opacity + "-moved " + Pixels(overlay, image, graphics));
                settings.Group.Title = "重命名后的分组 Renamed " + dpi;
                settings.Items[0].Label = "更新标签 " + opacity;
                settings.Group.Width = 900;
                overlay.RefreshItemsLayout();
                Console.WriteLine("PIXELS " + dpi + "-" + opacity + "-edited " + Pixels(overlay, image, graphics));
                settings.Group.Collapsed = true;
                Console.WriteLine("PIXELS " + dpi + "-" + opacity + "-collapsed " + Pixels(overlay, image, graphics));
                settings.Group.Collapsed = false;
            }
            // Changing more labels than the cache capacity must still draw and
            // dispose cleanly; long editing sessions may not grow without bound.
            for (int edit = 0; edit < 540; edit++)
            {
                settings.Group.Title = "编辑 " + edit;
                settings.Group.Collapsed = true;
                overlay.PaintSurface(graphics);
            }
            Expect(!overlay.IsHandleCreated && !overlay.Visible, "Rendering checks must not show or attach a desktop window");
        }
        Console.WriteLine("PASS: repeated paints, movement, edits, DPI, opacity, collapse and long editing sessions render without desktop windows");
    }

    private static void CheckClipping()
    {
        AppSettings settings = Settings(80);
        using (OverlayForm overlay = Overlay(settings))
        using (var image = new Bitmap(480, 320, PixelFormat.Format32bppPArgb))
        using (Graphics graphics = Graphics.FromImage(image))
        {
            foreach (int dpi in new[] { 96, 120, 144, 192 })
            {
                Layout(overlay, settings, dpi);
                foreach (DesktopItem item in settings.Items) { item.X -= 55; item.Y -= 33; }
                Console.WriteLine("PIXELS " + dpi + "-clipped " + Pixels(overlay, image, graphics));
                foreach (DesktopItem item in settings.Items) { item.X -= 17; item.Y -= 360; }
                Console.WriteLine("PIXELS " + dpi + "-entering " + Pixels(overlay, image, graphics));
            }
            Expect(!overlay.IsHandleCreated, "Clipping checks must stay offscreen");
        }
        Console.WriteLine("PASS: clipped edge tiles and entries moving into view preserve the visible image");
    }

    private static void Benchmark(int count)
    {
        AppSettings settings = Settings(count);
        using (OverlayForm overlay = Overlay(settings))
        using (var image = new Bitmap(1920, 1080, PixelFormat.Format32bppPArgb))
        using (Graphics graphics = Graphics.FromImage(image))
        {
            Layout(overlay, settings, 96);
            for (int warmup = 0; warmup < 8; warmup++) overlay.PaintSurface(graphics);
            double[] samples = new double[5];
            for (int sample = 0; sample < samples.Length; sample++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var stopwatch = Stopwatch.StartNew();
                for (int frame = 0; frame < 60; frame++)
                {
                    graphics.Clear(Color.Transparent);
                    int delta = frame % 2 == 0 ? 1 : -1;
                    settings.Group.X += delta;
                    foreach (DesktopItem item in settings.Items) item.X += delta;
                    overlay.PaintSurface(graphics);
                }
                stopwatch.Stop();
                samples[sample] = stopwatch.Elapsed.TotalMilliseconds / 60;
            }
            Array.Sort(samples);
            Console.WriteLine("BENCHMARK median_ms_per_frame=" + samples[2].ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                " items=" + count + " frame=1920x1080 dpi=96 samples=5 frames_per_sample=60");
        }
    }

    [STAThread]
    public static int Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try { CheckPixels(); CheckClipping(); Benchmark(80); Benchmark(600); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
