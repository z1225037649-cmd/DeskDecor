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
        private readonly Dictionary<string, Dictionary<int, Bitmap>> _scaled =
            new Dictionary<string, Dictionary<int, Bitmap>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IconStamp> _stamps =
            new Dictionary<string, IconStamp>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<string, Bitmap> _loadImage;

        public ShellIconCache() : this(LoadShellImage) { }

        internal ShellIconCache(Func<string, Bitmap> loadImage)
        {
            if (loadImage == null) throw new ArgumentNullException("loadImage");
            _loadImage = loadImage;
        }

        public Bitmap GetScaled(string path, int size)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException("size");
            Bitmap source = Get(path);
            Dictionary<int, Bitmap> sizes;
            if (!_scaled.TryGetValue(path, out sizes))
            {
                sizes = new Dictionary<int, Bitmap>();
                _scaled.Add(path, sizes);
            }
            Bitmap result;
            if (sizes.TryGetValue(size, out result)) return result;
            result = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            try
            {
                using (Graphics g = Graphics.FromImage(result))
                {
                    // Keep the Shell artwork and alpha unchanged. Dilating a
                    // dark outline closes the gaps in monochrome app icons.
                    g.CompositingMode = CompositingMode.SourceCopy;
                    if (source.Width == size && source.Height == size)
                        g.DrawImageUnscaled(source, 0, 0);
                    else
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(source, new Rectangle(0, 0, size, size));
                    }
                }
                sizes.Add(size, result);
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        public Bitmap Get(string path)
        {
            Bitmap image;
            if (_images.TryGetValue(path, out image)) return image;
            // Record metadata before loading: a replacement during extraction
            // must still be detected on the next refresh.
            IconStamp stamp = ReadStamp(path);
            image = _loadImage(path) ?? SystemIcons.Application.ToBitmap();
            _images.Add(path, image);
            _stamps.Add(path, stamp);
            return image;
        }

        public bool RefreshChanged()
        {
            var changed = new List<string>();
            foreach (KeyValuePair<string, IconStamp> entry in _stamps)
                if (!entry.Value.Equals(ReadStamp(entry.Key))) changed.Add(entry.Key);
            foreach (string path in changed)
            {
                _images[path].Dispose();
                _images.Remove(path);
                _stamps.Remove(path);
                Dictionary<int, Bitmap> sizes;
                if (_scaled.TryGetValue(path, out sizes))
                {
                    foreach (Bitmap image in sizes.Values) image.Dispose();
                    _scaled.Remove(path);
                }
            }
            return changed.Count != 0;
        }

        private struct IconStamp : IEquatable<IconStamp>
        {
            public bool Exists, IsDirectory;
            public long LastWriteTicks, Length;

            public bool Equals(IconStamp other)
            {
                return Exists == other.Exists && IsDirectory == other.IsDirectory &&
                    LastWriteTicks == other.LastWriteTicks && Length == other.Length;
            }
        }

        private static IconStamp ReadStamp(string path)
        {
            try
            {
                var file = new FileInfo(path);
                if (file.Exists) return new IconStamp { Exists = true,
                    LastWriteTicks = file.LastWriteTimeUtc.Ticks, Length = file.Length };
                var directory = new DirectoryInfo(path);
                if (directory.Exists) return new IconStamp { Exists = true, IsDirectory = true,
                    LastWriteTicks = directory.LastWriteTimeUtc.Ticks };
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
            catch (System.Security.SecurityException) { }
            return new IconStamp();
        }

        private static Bitmap LoadShellImage(string path)
        {
            Bitmap image;
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
            return image;
        }

        public void Dispose()
        {
            foreach (Bitmap image in _images.Values) image.Dispose();
            _images.Clear();
            foreach (Dictionary<int, Bitmap> sizes in _scaled.Values)
                foreach (Bitmap image in sizes.Values) image.Dispose();
            _scaled.Clear();
            _stamps.Clear();
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
