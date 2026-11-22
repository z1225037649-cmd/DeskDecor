using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace DeskDecor
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (!PrepareLocalUpgrade()) return;
            bool created;
            using (Mutex mutex = new Mutex(true, "DeskDecor.SingleInstance.20261114", out created))
            {
                if (!created)
                {
                    // A different running copy may hold the mutex even when the
                    // user chose an inactive configuration from another folder.
                    if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".migration", "pending-migration.xml")))
                    {
                        try
                        {
                            using (EventWaitHandle handoff = EventWaitHandle.OpenExisting(LocalUpgrade.PrepareEventName)) handoff.Set();
                            try { created = mutex.WaitOne(5000); }
                            catch (AbandonedMutexException) { created = true; }
                            if (created) goto StartApplication;
                        }
                        catch { }
                        MessageBox.Show("配置已迁移。请先从系统托盘退出仍在运行的旧版，然后重新打开新版。",
                            "DeskDecor 升级", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
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
                string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskDecor.settings.xml");
                try
                {
                    using (EventWaitHandle showSettings = new EventWaitHandle(false, EventResetMode.AutoReset,
                        "Local\\DeskDecor.ShowSettings.20261114"))
                    using (AppContext context = new AppContext(dataPath, showSettings))
                    {
                        context.StartUpgradeHandoff();
                        Application.Run(context);
                    }
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

        private static bool PrepareLocalUpgrade()
        {
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            LocalUpgradeResult result = LocalUpgrade.Prepare(directory, version);
            if (result.NeedsSourceSelection)
            {
                string source = SelectUpgradeSource(result.SourceDirectories);
                if (source == null) return false;
                result = LocalUpgrade.Prepare(directory, version, source);
            }
            if (result.NeedsOldExit && result.ExitRequested)
            {
                string source = result.SourceDirectory;
                for (int i = 0; i < 25 && result.NeedsOldExit; i++)
                {
                    Thread.Sleep(200);
                    result = LocalUpgrade.Prepare(directory, version, source);
                }
            }
            if (result.Failed || result.NeedsOldExit || result.NeedsSourceSelection)
            {
                MessageBox.Show(result.Message, "DeskDecor 升级", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            return true;
        }

        private static string SelectUpgradeSource(string[] directories)
        {
            using (Form dialog = new Form { Text = "选择要保留的旧版配置", ClientSize = new Size(680, 270),
                Font = new Font("Microsoft YaHei UI", 9F), StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false })
            {
                Label instructions = new Label { Text = "发现多份旧配置，请选择需要迁移的旧程序目录。",
                    AutoSize = true, Location = new Point(16, 18) };
                ListBox choices = new ListBox { Location = new Point(16, 52), Size = new Size(648, 148),
                    HorizontalScrollbar = true, IntegralHeight = false };
                choices.Items.AddRange(directories);
                Button accept = new Button { Text = "使用所选配置", DialogResult = DialogResult.OK,
                    Location = new Point(408, 220), Size = new Size(132, 32), Enabled = false };
                Button cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel,
                    Location = new Point(552, 220), Size = new Size(112, 32) };
                choices.SelectedIndexChanged += delegate { accept.Enabled = choices.SelectedIndex >= 0; };
                choices.DoubleClick += delegate { if (accept.Enabled) accept.PerformClick(); };
                dialog.Controls.Add(instructions); dialog.Controls.Add(choices);
                dialog.Controls.Add(accept); dialog.Controls.Add(cancel);
                dialog.AcceptButton = accept; dialog.CancelButton = cancel;
                return dialog.ShowDialog() == DialogResult.OK ? choices.SelectedItem as string : null;
            }
        }
    }
}
