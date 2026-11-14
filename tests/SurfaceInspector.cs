using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Read-only DWM preview of the running desktop surface, for visual diagnostics.
// Does not activate, move, reparent or change the source window.
internal sealed class SurfaceInspector : Form
{
    private IntPtr thumbnail;
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int L,T,R,B; }
    [StructLayout(LayoutKind.Sequential)] private struct Properties
    {
        public uint Flags; public Rect Destination, Source; public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
    }
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    private static extern IntPtr FindWindow(string cls, string name);
    [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
    [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref Properties p);
    [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(IntPtr thumb);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
    public SurfaceInspector()
    {
        Text = "DeskDecor 合成画面诊断";
        ClientSize = new Size(960, 580); BackColor = Color.FromArgb(50, 55, 60);
        StartPosition = FormStartPosition.CenterScreen;
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        IntPtr source = FindWindow(null, "DeskDecor.DesktopSurface");
        int cloaked;
        DwmGetWindowAttribute(source, 14, out cloaked, 4);
        int hr = DwmRegisterThumbnail(Handle, source, out thumbnail);
        Text += " | DWM=" + hr + " Cloaked=" + cloaked;
        Properties p = new Properties { Flags = 1|4|8|16,
            Destination = new Rect { L=0,T=0,R=960,B=540 },
            Opacity=255,Visible=true,ClientOnly=false };
        if (hr == 0) DwmUpdateThumbnailProperties(thumbnail, ref p);
    }
    protected override void Dispose(bool disposing)
    {
        if (thumbnail != IntPtr.Zero) DwmUnregisterThumbnail(thumbnail);
        base.Dispose(disposing);
    }
    [STAThread] public static void Main()
    {
        Application.EnableVisualStyles(); Application.Run(new SurfaceInspector());
    }
}
