using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace DeskDecor
{
    internal sealed class AppContext : ApplicationContext
    {
        private readonly string _dataPath;
        private readonly NotifyIcon _tray;
        private readonly Icon _appIcon;
        private readonly OverlayForm _overlay;
        private readonly DesktopFeatures _features;
        private readonly StartupRegistration _startup;
        private bool _startupEnabled;
        private bool _startupDisabledByMove;
        private string _startupCheckError;
        private string _settingsStatus;
        private bool _settingsSaveErrorShown;
        private bool _preserveRecoveredSettingsBackup;
        private readonly bool _settingsFileWasMissing;
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private readonly ShowSettingsMessageWindow _messageWindow;
        private readonly RegisteredWaitHandle _settingsWait;
        private ToolStripMenuItem _transparentTaskbarMenu;
        private ToolStripMenuItem _hideIconsMenu;
        private ToolStripMenuItem _startupMenu;
        private SettingsForm _settingsForm;
        private bool _visible = true;
        private bool _syncingFeatureMenus;

        public AppContext(string dataPath, EventWaitHandle showSettingsEvent)
        {
            _dataPath = dataPath;
            _settingsFileWasMissing = !File.Exists(dataPath);
            bool settingsRecovered;
            AppSettings settings = SettingsStore.Load(_dataPath, out _settingsStatus, out settingsRecovered);
            _preserveRecoveredSettingsBackup = settingsRecovered;
            _startup = new StartupRegistration(Application.ExecutablePath);
            CheckStartupLocation(false);
            _features = new DesktopFeatures();
            _features.SetTaskbarTransparent(settings.TaskbarTransparent);
            _features.SetDesktopIconsHidden(settings.DesktopIconsHidden);
            _overlay = new OverlayForm(settings);
            _overlay.ActiveGroupChanged += delegate
            {
                if (_settingsForm != null && !_settingsForm.IsDisposed)
                {
                    _settingsForm.RefreshItems();
                    _settingsForm.RefreshGroups();
                }
                SaveSettings(this, EventArgs.Empty);
            };
            _overlay.GroupTitleChanged += delegate
            {
                if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.RefreshGroups();
            };
            if (_overlay.LayoutWasRecovered)
                _settingsStatus = string.IsNullOrEmpty(_settingsStatus)
                    ? "检测到保存的分组位置或屏幕缩放发生变化，已恢复到当前屏幕可见区域。"
                    : _settingsStatus + " 分组位置也已恢复到当前屏幕。";
            _overlay.ItemActivated += OpenItem;
            _overlay.ItemMoved += SaveSettings;
            _overlay.ItemsChanged += delegate
            {
                if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.RefreshItems();
            };
            _overlay.Show();
            _messageWindow = new ShowSettingsMessageWindow();
            _messageWindow.ShowSettingsRequested += delegate { ShowSettings(false); };
            _messageWindow.TaskbarCreated += delegate { _features.TaskbarRecreated(); };
            _settingsWait = ThreadPool.RegisterWaitForSingleObject(showSettingsEvent, delegate
            {
                Native.PostMessage(_messageWindow.Handle, Native.WM_SHOW_SETTINGS, IntPtr.Zero, IntPtr.Zero);
            }, null, Timeout.Infinite, false);

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("添加快捷方式...", null, delegate { ShowSettings(false); _settingsForm.BeginAddShortcut(); });
            menu.Items.Add("添加文件夹...", null, delegate { ShowSettings(false); _settingsForm.BeginAddFolder(); });
            menu.Items.Add("管理桌面项目...", null, delegate { ShowSettings(false); });
            menu.Items.Add("显示/隐藏组件", null, delegate { ToggleVisible(); });
            _transparentTaskbarMenu = new ToolStripMenuItem("任务栏透明") { CheckOnClick = true, Checked = _features.TaskbarTransparent };
            _transparentTaskbarMenu.CheckedChanged += delegate
            {
                if (_syncingFeatureMenus) return;
                _features.SetTaskbarTransparent(_transparentTaskbarMenu.Checked);
                settings.TaskbarTransparent = _features.TaskbarTransparent;
                SaveSettings(this, EventArgs.Empty);
                SyncFeatureControls(_features.TaskbarTransparent, _features.DesktopIconsHidden);
            };
            menu.Items.Add(_transparentTaskbarMenu);
            _hideIconsMenu = new ToolStripMenuItem("隐藏系统桌面图标") { CheckOnClick = true, Checked = _features.DesktopIconsHidden };
            _hideIconsMenu.CheckedChanged += delegate
            {
                if (_syncingFeatureMenus) return;
                _features.SetDesktopIconsHidden(_hideIconsMenu.Checked);
                settings.DesktopIconsHidden = _hideIconsMenu.Checked;
                SaveSettings(this, EventArgs.Empty);
                SyncFeatureControls(_features.TaskbarTransparent, _hideIconsMenu.Checked);
            };
            menu.Items.Add(_hideIconsMenu);
            _startupMenu = new ToolStripMenuItem("开机自启动") { CheckOnClick = true, Checked = _startupEnabled };
            _startupMenu.CheckedChanged += delegate
            {
                if (!_syncingFeatureMenus) SetStartupEnabled(_startupMenu.Checked);
            };
            menu.Items.Add(_startupMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { ExitThread(); });

            _tray = new NotifyIcon();
            _tray.Text = "桌面小组件";
            _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            _tray.Icon = _appIcon;
            _tray.ContextMenuStrip = menu;
            _tray.Visible = true;
            _tray.DoubleClick += delegate { ShowSettings(false); };
            if (!string.IsNullOrEmpty(_settingsStatus))
                _tray.ShowBalloonTip(7000, "配置恢复提示", _settingsStatus, ToolTipIcon.Warning);
            if (_startupDisabledByMove) ShowStartupLocationNotice();
            // Keep the rolling backup intact on ordinary launches. A first-run
            // config and recovered off-screen layout are saved immediately.
            if (_settingsFileWasMissing || _overlay.LayoutWasRecovered)
                SaveSettings(this, EventArgs.Empty);

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _refreshTimer.Tick += delegate
            {
                _features.Refresh();
                CheckStartupLocation(true);
                _overlay.RefreshDesktopLayout();
                SyncFeatureControls(_features.TaskbarTransparent, _features.DesktopIconsHidden);
            };
            _refreshTimer.Start();
        }

        private void ShowSettings(bool addFolder)
        {
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm(_overlay.Settings);
                _settingsForm.SetPersistenceStatus(_settingsStatus);
                _settingsForm.SetAppearance(_overlay.Settings.IconSize, _overlay.Settings.Opacity);
                _settingsForm.SettingsChanged += delegate
                {
                    _overlay.RefreshItemsLayout();
                    SaveSettings(this, EventArgs.Empty);
                };
                _settingsForm.GroupChanged += delegate
                {
                    _overlay.SetActiveGroup(_overlay.Settings.ActiveGroupId);
                };
                _settingsForm.AppearanceChanged += delegate
                {
                    _overlay.Settings.IconSize = _settingsForm.IconSize;
                    _overlay.Settings.Opacity = _settingsForm.ItemOpacity;
                    _overlay.RefreshItemsLayout();
                    SaveSettings(this, EventArgs.Empty);
                };
                _settingsForm.AddGroupRequested += delegate
                {
                    _overlay.AddGroup("新分组 " + (_overlay.Settings.Groups.Count + 1));
                };
                _settingsForm.RemoveGroupRequested += delegate
                {
                    _overlay.RemoveActiveGroup();
                };
                _settingsForm.StartupChanged += delegate { SetStartupEnabled(_settingsForm.StartupEnabled); };
                _settingsForm.RenameGroupRequested += delegate
                {
                    _visible = true;
                    _settingsForm.SetOverlayVisible(true);
                    _settingsForm.Hide();
                    _overlay.ShowDesktopSurface();
                    _overlay.RenameGroup();
                };
                _settingsForm.ToggleOverlayRequested += delegate { ToggleVisible(); };
                _settingsForm.AutoArrangeRequested += delegate { _overlay.ArrangeItems(); };
                _settingsForm.DoneRequested += delegate
                {
                    if (_visible) _overlay.ShowDesktopSurface();
                };
                _settingsForm.TaskbarTransparencyChanged += delegate
                {
                    _features.SetTaskbarTransparent(!_features.TaskbarTransparent);
                    _overlay.Settings.TaskbarTransparent = _features.TaskbarTransparent;
                    SaveSettings(this, EventArgs.Empty);
                    SyncFeatureControls(_features.TaskbarTransparent, _features.DesktopIconsHidden);
                };
                _settingsForm.DesktopIconsVisibilityChanged += delegate
                {
                    _features.SetDesktopIconsHidden(!_features.DesktopIconsHidden);
                    _overlay.Settings.DesktopIconsHidden = _features.DesktopIconsHidden;
                    SaveSettings(this, EventArgs.Empty);
                    SyncFeatureControls(_features.TaskbarTransparent, _features.DesktopIconsHidden);
                };
            }
            _settingsForm.SetOverlayVisible(_visible);
            _overlay.SetActiveGroup(_overlay.Settings.ActiveGroupId);
            CheckStartupLocation(true);
            UpdateStartupControls();
            _settingsForm.SetFeatureStates(_features.TaskbarTransparent, _features.DesktopIconsHidden,
                _features.TaskbarStatus);
            _settingsForm.RefreshItems();
            _settingsForm.Show();
            // Settings and the desktop surface stay visible together for live
            // appearance edits, unless the user explicitly hides the widgets.
            if (_visible) _overlay.ShowDesktopSurface();
            _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.TopMost = true;
            Native.ShowWindow(_settingsForm.Handle, Native.SW_SHOW);
            Native.SetWindowPos(_settingsForm.Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            _settingsForm.Activate();
            Native.SetForegroundWindow(_settingsForm.Handle);
            if (addFolder) _settingsForm.BeginAddFolder();
        }

        private void SyncFeatureControls(bool transparentTaskbar, bool hideDesktopIcons)
        {
            _syncingFeatureMenus = true;
            try
            {
                if (_transparentTaskbarMenu != null) _transparentTaskbarMenu.Checked = transparentTaskbar;
                if (_hideIconsMenu != null) _hideIconsMenu.Checked = hideDesktopIcons;
                if (_startupMenu != null) _startupMenu.Checked = _startupEnabled;
            }
            finally { _syncingFeatureMenus = false; }
            if (_settingsForm != null && !_settingsForm.IsDisposed)
                _settingsForm.SetFeatureStates(transparentTaskbar, hideDesktopIcons, _features.TaskbarStatus);
        }

        private void SetStartupEnabled(bool enabled)
        {
            try
            {
                _startup.SetEnabled(enabled);
                _startupEnabled = _startup.Enabled;
                _startupDisabledByMove = false;
                _startupCheckError = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法修改开机自启动：\r\n" + ex.Message, "桌面小组件",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            UpdateStartupControls();
        }

        private void CheckStartupLocation(bool notify)
        {
            bool removed = false;
            try
            {
                removed = _startup.DisableIfLocationChanged();
                if (removed) _startupDisabledByMove = true;
                _startupEnabled = _startup.Enabled;
                _startupCheckError = null;
            }
            catch (Exception ex)
            {
                _startupCheckError = "无法检查或清理自启动：" + ex.Message;
            }
            UpdateStartupControls();
            if (removed && notify && _tray != null) ShowStartupLocationNotice();
        }

        private void ShowStartupLocationNotice()
        {
            _tray.ShowBalloonTip(6000, "开机自启动已关闭",
                "检测到软件位置已改变，已清除旧的自启动记录。需要时可在新位置重新开启。", ToolTipIcon.Info);
        }

        private void UpdateStartupControls()
        {
            _syncingFeatureMenus = true;
            try
            {
                if (_startupMenu != null)
                {
                    _startupMenu.Checked = _startupEnabled;
                    _startupMenu.Text = _startupDisabledByMove ? "开机自启动（位置改变，已关闭）" : "开机自启动";
                }
            }
            finally { _syncingFeatureMenus = false; }
            if (_settingsForm != null && !_settingsForm.IsDisposed)
                _settingsForm.SetStartupEnabled(_startupEnabled, _startupCheckError ??
                    (_startupDisabledByMove ? "软件位置已改变，自启动已关闭。可在新位置重新开启。" : null));
        }

        private void SaveSettings(object sender, EventArgs e)
        {
            try
            {
                SettingsStore.Save(_dataPath, _overlay.Settings, _preserveRecoveredSettingsBackup);
                _preserveRecoveredSettingsBackup = false;
                _settingsStatus = null;
                _settingsSaveErrorShown = false;
                if (_settingsForm != null && !_settingsForm.IsDisposed)
                    _settingsForm.SetPersistenceStatus(null);
                _overlay.InvalidateSurface();
            }
            catch (Exception ex)
            {
                _settingsStatus = "配置保存失败，旧配置和备份已保留：" + ex.Message;
                if (_settingsForm != null && !_settingsForm.IsDisposed)
                    _settingsForm.SetPersistenceStatus(_settingsStatus);
                if (!_settingsSaveErrorShown && _tray != null)
                {
                    _settingsSaveErrorShown = true;
                    _tray.ShowBalloonTip(6000, "设置未能保存", _settingsStatus, ToolTipIcon.Error);
                }
            }
        }

        private void OpenItem(object sender, DesktopItem item)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.Path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开项目：\r\n" + ex.Message, "桌面小组件",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ToggleVisible()
        {
            _visible = !_visible;
            if (_visible) _overlay.ShowDesktopSurface();
            else _overlay.Hide();
            if (_settingsForm != null && !_settingsForm.IsDisposed)
                _settingsForm.SetOverlayVisible(_visible);
        }

        protected override void ExitThreadCore()
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            if (_settingsWait != null) _settingsWait.Unregister(null);
            _features.Dispose();
            _messageWindow.Dispose();
            if (_settingsForm != null) _settingsForm.Dispose();
            _overlay.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _appIcon.Dispose();
            base.ExitThreadCore();
        }
    }

    internal sealed class ShowSettingsMessageWindow : NativeWindow, IDisposable
    {
        private readonly uint _taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        public event EventHandler ShowSettingsRequested;
        public event EventHandler TaskbarCreated;

        public ShowSettingsMessageWindow()
        {
            CreateHandle(new CreateParams { Caption = "DeskDecor.InstanceMessageWindow" });
        }

        protected override void WndProc(ref Message m)
        {
            if ((uint)m.Msg == _taskbarCreated)
            {
                if (TaskbarCreated != null) TaskbarCreated(this, EventArgs.Empty);
                return;
            }
            if (m.Msg == Native.WM_SHOW_SETTINGS)
            {
                if (ShowSettingsRequested != null) ShowSettingsRequested(this, EventArgs.Empty);
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero) DestroyHandle();
        }
    }
}
