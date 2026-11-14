using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace DeskDecor
{
    // Compile DesktopFeatures.cs against this fake boundary. Tests never modify Explorer.
    internal static class TaskbarRegression
    {
        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        public static void Main()
        {
            Native.Reset(false);
            using (DesktopFeatures features = new DesktopFeatures())
            {
                features.SetTaskbarTransparent(false);
                Expect(Native.AccentWrites == 0, "Starting disabled must not touch the taskbar");
                features.SetTaskbarTransparent(true);
                Expect(features.TaskbarTransparent && Native.AccentWrites == 1, "Enable once");
                for (int i = 0; i < 50; i++)
                {
                    features.Refresh();
                    features.SetTaskbarTransparent(true);
                }
                Expect(Native.AccentWrites == 1, "Refresh and duplicate enable must not reapply accent");
                Expect(Native.PositionWrites == 0, "Taskbar effects must not force frame changes");
                features.SetTaskbarTransparent(false);
                features.SetTaskbarTransparent(false);
                Expect(Native.AccentWrites == 2, "Disable must restore exactly once");
                Expect(Native.LastPolicy.AccentState == 3 && Native.LastPolicy.GradientColor == 123,
                    "Disable must preserve the original policy");
            }
            Expect(Native.AccentWrites == 2, "Disposal after disable must not reset again");
            Console.WriteLine("PASS: enable/refresh/disable produces no repeated accent or frame writes");

            Native.Reset(true);
            XamlTaskbarEngine.EnableCalls = XamlTaskbarEngine.RestoreCalls = 0;
            using (DesktopFeatures features = new DesktopFeatures())
            {
                features.SetTaskbarTransparent(true);
                for (int i = 0; i < 50; i++)
                {
                    features.Refresh();
                    features.SetTaskbarTransparent(true);
                }
                Expect(features.TaskbarTransparent, "XAML must route through the dedicated engine");
                Expect(XamlTaskbarEngine.EnableCalls == 1, "XAML engine must not be called by periodic refresh");
                Expect(Native.AccentWrites == 0 && Native.PositionWrites == 0,
                    "XAML must not use the legacy accent or frame APIs");
                features.TaskbarRecreated();
                Expect(XamlTaskbarEngine.EnableCalls == 2, "Explorer recreation must reconnect XAML once");
                features.SetTaskbarTransparent(false);
                Expect(XamlTaskbarEngine.RestoreCalls == 1, "Disabling XAML restores the saved fills once");
            }
            Expect(Native.AccentWrites == 0, "XAML never falls back to unsupported accent requests");
            Console.WriteLine("PASS: XAML uses the dedicated engine once and restores when disabled");

            Native.Reset(false);
            Rectangle screen = new Rectangle(0, 0, 1920, 1080);
            Expect(DesktopFeatures.FindDesktopParent() == Native.Progman,
                "Hidden WorkerW windows must not be selected as the desktop host");
            for (int i = 0; i < 50; i++)
                DesktopFeatures.AttachOverlayToDesktop(Native.Overlay, screen);
            Expect(Native.WorkerMessages == 0, "Desktop overlay must not create wallpaper workers");
            Expect(Native.PositionWrites == 0, "Stable desktop attachment must not move/show/reset frames");
            Native.OverlayStyle = Native.WS_CHILD;
            Native.OverlayOwner = IntPtr.Zero;
            Native.DesktopPrevious = Native.Tray;
            DesktopFeatures.AttachOverlayToDesktop(Native.Overlay, screen);
            Expect((Native.OverlayStyle & Native.WS_CHILD) == 0 &&
                (Native.OverlayStyle & Native.WS_POPUP) != 0, "Surface must be a layered popup, not a child");
            Expect(Native.OverlayOwner == Native.Progman && Native.LastAfter == Native.Tray,
                "Popup must be owned by desktop and inserted below ordinary apps");
            for (int i = 0; i < 50; i++)
                DesktopFeatures.AttachOverlayToDesktop(Native.Overlay, screen);
            Expect(Native.PositionWrites == 1, "Repaired surface must not churn the z-order");
            Native.DesktopRaised = true;
            Native.DesktopPrevious = Native.Overlay;
            DesktopFeatures.AttachOverlayToDesktop(Native.Overlay, screen);
            Expect(!Native.DesktopRaised && Native.DesktopLowerings == 1,
                "A desktop owner raised by title editing must be lowered beneath ordinary apps");
            for (int i = 0; i < 50; i++) DesktopFeatures.AttachOverlayToDesktop(Native.Overlay, screen);
            Expect(Native.DesktopLowerings == 1 && Native.PositionWrites == 3,
                "Repairing activation must settle without repeated desktop or overlay movement");
            Expect((Native.LastFlags & Native.SWP_NOOWNERZORDER) != 0,
                "Placing the overlay must not raise its owner chain");
            Native.OverlayExtendedStyle = 8;
            DesktopFeatures.AttachOverlayToDesktop(Native.Overlay, screen);
            Expect(Native.OverlayExtendedStyle == 0 && Native.TopmostRemovals == 1,
                "A stale TopMost flag must be explicitly removed, even when bounds and owner look correct");
            Console.WriteLine("PASS: stable desktop refresh avoids WorkerW and window-position churn");
        }
    }

    internal sealed class XamlTaskbarEngine : IDisposable
    {
        public static int EnableCalls, RestoreCalls;
        private readonly Action<bool, string> _changed;
        public XamlTaskbarEngine(Action<bool, string> changed) { _changed = changed; }
        public void Apply(bool enabled, System.Collections.Generic.List<IntPtr> taskbars)
        {
            if (enabled) EnableCalls++; else RestoreCalls++;
            _changed(enabled, enabled ? "XAML test connected" : "disabled");
        }
        public void Dispose() { }
        public void ResetConnection() { }
    }

    internal static class Native
    {
        public static readonly IntPtr Tray = new IntPtr(1), Xaml = new IntPtr(2),
            Progman = new IntPtr(3), DefView = new IntPtr(4), Worker = new IntPtr(5), Overlay = new IntPtr(6), App = new IntPtr(7);
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        public const int ACCENT_DISABLED = 0, ACCENT_ENABLE_TRANSPARENTGRADIENT = 2, WCA_ACCENT_POLICY = 19;
        public const int GWL_STYLE = -16, WS_CHILD = 0x40000000, WS_POPUP = unchecked((int)0x80000000);
        public const int GWLP_HWNDPARENT = -8;
        public const uint GW_HWNDPREV = 3, GW_OWNER = 4;
        public static int OverlayStyle;
        public static int OverlayExtendedStyle, TopmostRemovals;
        public static IntPtr OverlayOwner, DesktopPrevious, LastAfter;
        public static bool DesktopRaised;
        public static int DesktopLowerings;
        public static uint LastFlags;
        public const uint SW_HIDE = 0, SW_SHOW = 5, SMTO_ABORTIFHUNG = 2,
            SWP_NOACTIVATE = 16, SWP_FRAMECHANGED = 32, SWP_SHOWWINDOW = 64, GW_HWNDNEXT = 2;
        public const uint SWP_NOMOVE = 2, SWP_NOSIZE = 1, SWP_NOOWNERZORDER = 512;
        public static int AccentWrites, PositionWrites, WorkerMessages;
        private static bool _xaml;
        public static ACCENTPOLICY LastPolicy;
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr ignored);
        [StructLayout(LayoutKind.Sequential)]
        public struct ACCENTPOLICY { public int AccentState, AccentFlags, GradientColor, AnimationId; }
        public struct WINDOWCOMPOSITIONATTRIBDATA { public int Attribute; public IntPtr Data; public int SizeOfData; }
        public struct POINT { public int x, y; public POINT(int px, int py) { x = px; y = py; } }
        public struct RECT { public int left, top, right, bottom; }
        public static void Reset(bool xaml)
        {
            _xaml = xaml; AccentWrites = PositionWrites = WorkerMessages = 0;
            OverlayStyle = WS_POPUP; OverlayOwner = Progman; DesktopPrevious = Overlay;
            DesktopRaised = false; DesktopLowerings = 0;
            OverlayExtendedStyle = TopmostRemovals = 0;
        }
        public static IntPtr FindWindow(string name, string title)
        {
            return name == "Shell_TrayWnd" ? Tray : name == "Progman" ? Progman : IntPtr.Zero;
        }
        public static IntPtr FindWindowEx(IntPtr parent, IntPtr after, string name, string title)
        {
            if (parent == Progman && name == "SHELLDLL_DefView") return DefView;
            if (parent == IntPtr.Zero && after == Progman && name == "WorkerW") return Worker;
            return IntPtr.Zero;
        }
        public static bool EnumWindows(EnumWindowsProc action, IntPtr ignored)
        {
            foreach (IntPtr handle in DesktopRaised ? new [] { Worker, Tray, Overlay, Progman, App } : new [] { Worker, Tray, App, Overlay, Progman })
                if (!action(handle, ignored)) break;
            return true;
        }
        public static bool EnumChildWindows(IntPtr parent, EnumWindowsProc action, IntPtr ignored)
        {
            if (_xaml && parent == Tray) action(Xaml, ignored);
            return true;
        }
        public static bool EnumThreadWindows(uint thread, EnumWindowsProc action, IntPtr ignored) { return true; }
        public static int GetClassName(IntPtr hwnd, StringBuilder name, int capacity)
        {
            name.Append(hwnd == Tray ? "Shell_TrayWnd" : hwnd == Xaml
                ? "Windows.UI.Composition.DesktopWindowContentBridge" : hwnd == Progman ? "Progman" : hwnd == App ? "Chrome_WidgetWin_1" : "WorkerW");
            return name.Length;
        }
        public static bool GetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data)
        {
            Marshal.StructureToPtr(new ACCENTPOLICY { AccentState = 3, GradientColor = 123 }, data.Data, false);
            return true;
        }
        public static bool SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data)
        {
            if (hwnd != Tray) throw new Exception("Taskbar children must not be modified");
            AccentWrites++;
            LastPolicy = (ACCENTPOLICY)Marshal.PtrToStructure(data.Data, typeof(ACCENTPOLICY));
            return true;
        }
        public static bool IsWindow(IntPtr hwnd) { return true; }
        public static bool IsWindowVisible(IntPtr hwnd) { return hwnd != Worker; }
        public static bool ShowWindow(IntPtr hwnd, uint state) { return true; }
        public static IntPtr GetShellWindow() { return Progman; }
        public static IntPtr GetDesktopWindow() { return new IntPtr(99); }
        public static IntPtr GetWindow(IntPtr hwnd, uint command)
        {
            if (hwnd == Overlay && command == GW_OWNER) return OverlayOwner;
            if (hwnd == Progman && command == GW_HWNDPREV) return DesktopPrevious;
            return IntPtr.Zero;
        }
        public static IntPtr GetParent(IntPtr hwnd) { return hwnd == Overlay ? Progman : IntPtr.Zero; }
        public static int GetWindowLongStyle(IntPtr hwnd, int index)
        { return hwnd == Overlay ? (index == GWL_STYLE ? OverlayStyle : OverlayExtendedStyle) : 0; }
        public static int SetWindowLongStyle(IntPtr hwnd, int index, int style)
        { if (index == GWL_STYLE) OverlayStyle = style; return 0; }
        public static IntPtr SetWindowLongPointer(IntPtr hwnd, int index, IntPtr owner)
        { OverlayOwner = owner; return IntPtr.Zero; }
        public static IntPtr SetParent(IntPtr hwnd, IntPtr parent) { return IntPtr.Zero; }
        public static bool ScreenToClient(IntPtr hwnd, ref POINT point) { return true; }
        public static bool GetWindowRect(IntPtr hwnd, out RECT rect)
        {
            rect = new RECT { left = 0, top = 0, right = 1920, bottom = 1080 }; return true;
        }
        public static bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags)
        {
            PositionWrites++;
            if (hwnd == Overlay && after == new IntPtr(-2))
            { TopmostRemovals++; OverlayExtendedStyle &= ~8; return true; }
            if (hwnd == Progman) { DesktopLowerings++; DesktopRaised = false; DesktopPrevious = App; return true; }
            LastAfter = after; LastFlags = flags; DesktopPrevious = Overlay; return true;
        }
        public static IntPtr SendMessageTimeoutPtr(IntPtr hwnd, uint msg, IntPtr wp, IntPtr lp, uint flags,
            uint timeout, out IntPtr result)
        {
            WorkerMessages++; result = IntPtr.Zero; return new IntPtr(1);
        }
    }
}
