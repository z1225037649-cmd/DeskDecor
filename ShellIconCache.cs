using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace DeskDecor
{
    // Shell icons include real folder/file-type artwork. Cache them so hover and
    // dragging only redraw pixels, without repeatedly calling into the shell.
    internal sealed class ShellIconCache : IDisposable
    {
        private readonly Dictionary<string, Bitmap> _images =
            new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Bitmap> _outlined =
            new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        public Bitmap GetOutlined(string path, int size)
        {
            string key = size + "|" + path;
            Bitmap result;
            if (_outlined.TryGetValue(key, out result)) return result;
            using (var scaled = new Bitmap(size, size, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(Get(path), new Rectangle(0, 0, size, size));
                }
                result = new Bitmap(size + 4, size + 4, PixelFormat.Format32bppPArgb);
                // Dilate the alpha silhouette, never the rectangular image bounds.
                // Taking the maximum avoids dark build-up along detailed edges.
                for (int y = 0; y < result.Height; y++)
                    for (int x = 0; x < result.Width; x++)
                    {
                        int alpha = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int sx = x - 2 + dx, sy = y - 2 + dy;
                                if (sx >= 0 && sx < size && sy >= 0 && sy < size)
                                    alpha = Math.Max(alpha, scaled.GetPixel(sx, sy).A * 150 / 255);
                            }
                        int shadowX = x - 2, shadowY = y - 4;
                        if (shadowX >= 0 && shadowX < size && shadowY >= 0 && shadowY < size)
                            alpha = Math.Max(alpha, scaled.GetPixel(shadowX, shadowY).A * 60 / 255);
                        if (alpha > 0) result.SetPixel(x, y, Color.FromArgb(alpha, 0, 0, 0));
                    }
                using (Graphics g = Graphics.FromImage(result)) g.DrawImageUnscaled(scaled, 2, 2);
            }
            _outlined[key] = result;
            return result;
        }

        public Bitmap Get(string path)
        {
            Bitmap image;
            if (_images.TryGetValue(path, out image)) return image;
            SHFILEINFO info = new SHFILEINFO();
            uint flags = 0x100; // SHGFI_ICON | SHGFI_LARGEICON (32px source)
            uint attributes = Directory.Exists(path) ? 0x10U : 0x80U;
            if (!Directory.Exists(path) && !File.Exists(path)) flags |= 0x10; // USEFILEATTRIBUTES
            try
            {
                SHGetFileInfo(path, attributes, ref info, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), flags);
                if (info.Icon != IntPtr.Zero)
                {
                    using (Icon icon = Icon.FromHandle(info.Icon)) image = icon.ToBitmap();
                }
                else image = (attributes == 0x10 ? SystemIcons.WinLogo : SystemIcons.Application).ToBitmap();
            }
            catch { image = SystemIcons.Application.ToBitmap(); }
            finally { if (info.Icon != IntPtr.Zero) DestroyIcon(info.Icon); }
            _images[path] = image;
            return image;
        }

        public void Dispose()
        {
            foreach (Bitmap image in _images.Values) image.Dispose();
            _images.Clear();
            foreach (Bitmap image in _outlined.Values) image.Dispose();
            _outlined.Clear();
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr Icon;
            public int IconIndex;
            public uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref SHFILEINFO info, uint size, uint flags);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    }
}
