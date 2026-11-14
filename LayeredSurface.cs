using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DeskDecor
{
    internal sealed class LayeredSurface : IDisposable
    {
        private IntPtr _bitmap;
        private IntPtr _oldBitmap;
        private IntPtr _memoryDc;
        private Bitmap _surface;
        private int _width;
        private int _height;
        public int LastError { get; private set; }

        public Bitmap Begin(int width, int height)
        {
            if (width <= 0 || height <= 0) return null;
            if (_surface != null && _width == width && _height == height) return _surface;
            Release();

            Native.BITMAPINFOHEADER info = new Native.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER)),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Native.BI_RGB
            };

            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            try
            {
                IntPtr bits;
                _bitmap = Native.CreateDIBSection(screenDc, ref info, Native.DIB_RGB_COLORS,
                    out bits, IntPtr.Zero, 0);
                if (_bitmap == IntPtr.Zero || bits == IntPtr.Zero) return null;
                _memoryDc = Native.CreateCompatibleDC(screenDc);
                _oldBitmap = Native.SelectObject(_memoryDc, _bitmap);
                _surface = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, bits);
                _width = width;
                _height = height;
                return _surface;
            }
            catch { Release(); return null; }
            finally { Native.ReleaseDC(IntPtr.Zero, screenDc); }
        }

        public bool Push(IntPtr hwnd, int x, int y)
        {
            if (_surface == null || _memoryDc == IntPtr.Zero) return false;
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            try
            {
                Native.POINT dst = new Native.POINT(x, y);
                Native.SIZE size = new Native.SIZE(_width, _height);
                Native.POINT src = new Native.POINT(0, 0);
                Native.BLENDFUNCTION blend = new Native.BLENDFUNCTION
                {
                    BlendOp = Native.AC_SRC_OVER,
                    SourceConstantAlpha = 255,
                    AlphaFormat = Native.AC_SRC_ALPHA
                };
                bool result = Native.UpdateLayeredWindow(hwnd, screenDc, ref dst, ref size, _memoryDc,
                    ref src, 0, ref blend, Native.ULW_ALPHA);
                LastError = result ? 0 : Marshal.GetLastWin32Error();
                return result;
            }
            finally { Native.ReleaseDC(IntPtr.Zero, screenDc); }
        }

        private void Release()
        {
            if (_surface != null) { _surface.Dispose(); _surface = null; }
            if (_memoryDc != IntPtr.Zero)
            {
                if (_oldBitmap != IntPtr.Zero) Native.SelectObject(_memoryDc, _oldBitmap);
                Native.DeleteDC(_memoryDc);
                _memoryDc = IntPtr.Zero;
                _oldBitmap = IntPtr.Zero;
            }
            if (_bitmap != IntPtr.Zero) { Native.DeleteObject(_bitmap); _bitmap = IntPtr.Zero; }
            _width = 0;
            _height = 0;
        }

        public void Dispose() { Release(); }
            }
}
