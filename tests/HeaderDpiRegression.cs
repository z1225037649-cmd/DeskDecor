using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using DeskDecor;

internal static class HeaderDpiRegression
{
    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    [STAThread] private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try
        {
            foreach (int dpi in new[] { 96, 120, 144, 192 })
            {
                var settings = new AppSettings { DesktopInitialized = true };
                using (var overlay = new OverlayForm(settings))
                using (var bitmap = new Bitmap(300, 100))
                using (var graphics = Graphics.FromImage(bitmap))
                using (var font = new Font("Microsoft YaHei UI", MonitorDpi.Scale(12, dpi), FontStyle.Regular, GraphicsUnit.Pixel))
                using (var format = new StringFormat { LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                {
                    settings.Group.Dpi = dpi;
                    Rectangle text = (Rectangle)typeof(OverlayForm).GetMethod("CollapseLabelBounds",
                        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(overlay, null);
                    Rectangle button = (Rectangle)typeof(OverlayForm).GetMethod("CollapseButtonBounds",
                        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(overlay, null);
                    Expect(text.Left >= button.Left && text.Top >= button.Top && text.Bottom <= button.Bottom &&
                        text.Right <= button.Right - MonitorDpi.Scale(18, dpi), "Collapse text overlaps its chevron at DPI " + dpi);
                    foreach (string label in new[] { "收起", "展开" })
                    {
                        int characters, lines;
                        graphics.MeasureString(label, font, new SizeF(text.Width, text.Height), format, out characters, out lines);
                        Expect(characters == label.Length && lines == 1, "Collapse/expand label is clipped at DPI " + dpi);
                    }
                    Expect(!overlay.Visible && !overlay.IsHandleCreated, "Header measurement must not show or attach a window");
                }
            }
            Console.WriteLine("PASS: collapse and expand labels fit at 100%, 125%, 150% and 200% without overlapping the chevron");
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error.Message); Environment.ExitCode = 1; }
    }
}
