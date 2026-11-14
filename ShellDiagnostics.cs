using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DeskDecor
{
    internal static class ShellDiagnostics
    {
        public static void Write()
        {
            StringBuilder text = new StringBuilder();
            int z = 0;
            int lastApplication = -1, overlayPosition = -1, desktopPosition = -1;
            Native.EnumWindows(delegate(IntPtr hwnd, IntPtr ignored)
            {
                int position = z++;
                string name = ClassName(hwnd);
                if (Title(hwnd) == "DeskDecor.DesktopSurface") overlayPosition = position;
                if (Native.IsWindowVisible(hwnd) && Native.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                    desktopPosition = position;
                if (Native.IsWindowVisible(hwnd) && name != "WorkerW" && name != "Progman" &&
                    !name.Contains("TrayWnd") && Title(hwnd) != "DeskDecor.DesktopSurface" &&
                    (Native.GetWindowLongStyle(hwnd, -20) & 0x80) == 0)
                    lastApplication = position;
                if (name == "Progman" || name == "WorkerW" || name.Contains("TrayWnd") ||
                    Title(hwnd).Contains("DeskDecor"))
                {
                    text.AppendLine("z=" + position + " " + Describe(hwnd));
                    Native.EnumChildWindows(hwnd, delegate(IntPtr child, IntPtr unused)
                    {
                        text.AppendLine("    " + Describe(child));
                        return true;
                    }, IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);
            text.AppendLine("desktopOrder lastApplication=" + lastApplication + " overlay=" + overlayPosition +
                " desktop=" + desktopPosition + " belowApps=" + (overlayPosition > lastApplication) +
                " aboveDesktop=" + (overlayPosition >= 0 && overlayPosition < desktopPosition));
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskDecor.shell.txt"), text.ToString());
        }

        private static string Describe(IntPtr hwnd)
        {
            Native.RECT rect;
            Native.GetWindowRect(hwnd, out rect);
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return string.Format("hwnd={0} parent={1} pid={2} visible={3} class={4} title={5} rect={6},{7},{8},{9} style={10:X8} ex={11:X8}",
                hwnd, Native.GetParent(hwnd), pid, Native.IsWindowVisible(hwnd), ClassName(hwnd), Title(hwnd),
                rect.left, rect.top, rect.right, rect.bottom, Native.GetWindowLongStyle(hwnd, -16),
                Native.GetWindowLongStyle(hwnd, -20));
        }
        private static string ClassName(IntPtr hwnd)
        {
            StringBuilder value = new StringBuilder(256);
            Native.GetClassName(hwnd, value, value.Capacity);
            return value.ToString();
        }
        private static string Title(IntPtr hwnd)
        {
            StringBuilder value = new StringBuilder(256);
            GetWindowText(hwnd, value, value.Capacity);
            return value.ToString();
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hwnd, StringBuilder value, int length);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    }
}
