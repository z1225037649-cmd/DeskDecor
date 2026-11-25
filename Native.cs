using System;
using System.Runtime.InteropServices;

namespace DeskDecor
{
    internal static class Native
    {
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_SHOW_SETTINGS = 0x8000 + 41;
        public const uint MB_OK = 0x00000000;
        public const uint MB_ICONINFORMATION = 0x00000040;
        public const uint MB_ICONERROR = 0x00000010;
        public const uint MB_TOPMOST = 0x00040000;
        public const uint MB_SETFOREGROUND = 0x00010000;
        public const int HTTRANSPARENT = -1;
        public const int HTCLIENT = 1;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int GWL_STYLE = -16;
        public const int WS_CHILD = unchecked((int)0x40000000);
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const uint SW_HIDE = 0;
        public const uint SW_SHOW = 5;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_NOOWNERZORDER = 0x0200;
        public const uint GW_HWNDNEXT = 2;
        public const uint GW_HWNDPREV = 3;
        public const uint GW_OWNER = 4;
        public const int GWLP_HWNDPARENT = -8;
        public const uint SMTO_ABORTIFHUNG = 0x0002;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        public const int WCA_ACCENT_POLICY = 19;
        public const int ACCENT_DISABLED = 0;
        public const int ACCENT_ENABLE_TRANSPARENTGRADIENT = 2;
        public const int ULW_ALPHA = 0x00000002;
        public const byte AC_SRC_OVER = 0x00;
        public const byte AC_SRC_ALPHA = 0x01;
        public const uint DIB_RGB_COLORS = 0;
        public const uint BI_RGB = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x, y; public POINT(int px, int py) { x = px; y = py; } }
        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx, cy; public SIZE(int w, int h) { cx = w; cy = h; } }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter;
            public uint biClrUsed, biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWCOMPOSITIONATTRIBDATA
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ACCENTPOLICY
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowName);
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder className, int maxCount);
        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hwnd);
        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern uint RegisterWindowMessage(string message);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hwnd, string text, string caption, uint type);
        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int virtualKey);
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        public static extern int GetWindowLongStyle(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        public static extern int SetWindowLongStyle(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        public static extern IntPtr SetWindowLongPointer(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")]
        public static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll", EntryPoint = "SendMessageTimeout", SetLastError = true)]
        public static extern IntPtr SendMessageTimeoutPtr(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam,
            uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);
        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hwnd, ref POINT point);
        [DllImport("user32.dll")]
        public static extern bool GetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT dst,
            ref SIZE size, IntPtr hdcSrc, ref POINT src, int colorKey, ref BLENDFUNCTION blend, int flags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll", SetLastError = true)] public static extern IntPtr CreateDIBSection(
            IntPtr hdc, ref BITMAPINFOHEADER info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);

    }
}
