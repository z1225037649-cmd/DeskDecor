using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace DeskDecor
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "DeskDecor.SingleInstance.20261114", out created))
            {
                if (!created)
                {
                    try
                    {
                        using (EventWaitHandle showSettings = EventWaitHandle.OpenExisting(
                            "Local\\DeskDecor.ShowSettings.20261114"))
                        {
                            showSettings.Set();
                            return;
                        }
                    }
                    catch { }

                    IntPtr messageWindow = Native.FindWindow(null, "DeskDecor.InstanceMessageWindow");
                    bool notified = messageWindow != IntPtr.Zero && Native.PostMessage(messageWindow,
                        Native.WM_SHOW_SETTINGS, IntPtr.Zero, IntPtr.Zero);
                    if (notified) return;

                    // The mutex can outlive the window lookup briefly during startup or
                    // when the existing instance is closing. Never kill another instance.
                    try
                    {
                        created = mutex.WaitOne(1500);
                        if (created) goto StartApplication;
                    }
                    catch (AbandonedMutexException)
                    {
                        // An abandoned mutex is acquired by the waiting thread.
                        created = true;
                        goto StartApplication;
                    }
                    catch { }
                    Native.MessageBox(IntPtr.Zero, "桌面小组件已在运行，但无法唤醒设置窗口。请从系统托盘打开。",
                        "桌面小组件", Native.MB_OK | Native.MB_ICONINFORMATION | Native.MB_TOPMOST | Native.MB_SETFOREGROUND);
                    return;
                }
StartApplication:
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskDecor.settings.xml");
                try
                {
                    using (EventWaitHandle showSettings = new EventWaitHandle(false, EventResetMode.AutoReset,
                        "Local\\DeskDecor.ShowSettings.20261114"))
                        Application.Run(new AppContext(dataPath, showSettings));
                }
                catch (Exception ex)
                {
                    string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskDecor.startup.log");
                    try { File.WriteAllText(logPath, ex.ToString()); } catch { }
                    Native.MessageBox(IntPtr.Zero, "启动失败，详情已写入：\r\n" + logPath + "\r\n\r\n" + ex.Message,
                        "桌面小组件", Native.MB_OK | Native.MB_ICONERROR | Native.MB_TOPMOST | Native.MB_SETFOREGROUND);
                }
                GC.KeepAlive(mutex);
            }
        }
    }
}
