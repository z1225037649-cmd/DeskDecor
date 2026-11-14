using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DeskDecor
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private readonly ListView _list;
        private readonly Button _toggleOverlay;
        private readonly CheckBox _transparentTaskbar;
        private readonly CheckBox _hideDesktopIcons;
        private readonly CheckBox _startup;
        private readonly Label _startupStatus;
        private readonly Label _taskbarStatus;
        private bool _syncingFeatures;
        public event EventHandler SettingsChanged;
        public event EventHandler ToggleOverlayRequested;
        public event EventHandler AutoArrangeRequested;
        public event EventHandler TaskbarTransparencyChanged;
        public event EventHandler DesktopIconsVisibilityChanged;
        public event EventHandler DoneRequested;
        public event EventHandler StartupChanged;
        public event EventHandler RenameGroupRequested;
        public bool StartupEnabled { get { return _startup.Checked; } }

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            Text = "桌面小组件";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 634);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(246, 248, 250);
            ForeColor = Color.FromArgb(37, 46, 58);
            Controls.Add(new Label { Text = "桌面小组件", AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold), Location = new Point(22, 20) });
            Controls.Add(new Label { Text = "常用文件，放在触手可及的地方。", AutoSize = true,
                ForeColor = Color.FromArgb(111, 121, 134), Location = new Point(25, 61) });

            _list = new ListView { View = View.Details, FullRowSelect = true, MultiSelect = true,
                HideSelection = false, BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White, ForeColor = ForeColor, Location = new Point(24, 98), Size = new Size(512, 206) };
            _list.Columns.Add("名称", 190);
            _list.Columns.Add("位置", 294);
            Controls.Add(_list);

            Button addLink = Button("＋ 添加文件", 24, 318, delegate { AddShortcut(); });
            Button addFolder = Button("添加文件夹", 199, 318, delegate { BeginAddFolder(); });
            Button remove = Button("移除选中", 374, 318, delegate { RemoveSelected(); });
            Button arrange = Button("自动排列", 24, 362, delegate
            {
                if (AutoArrangeRequested != null) AutoArrangeRequested(this, EventArgs.Empty);
            });
            _toggleOverlay = Button("隐藏组件", 199, 362, delegate
            {
                if (ToggleOverlayRequested != null) ToggleOverlayRequested(this, EventArgs.Empty);
            });
            Button rename = Button("修改分组名称", 374, 362, delegate
            {
                if (RenameGroupRequested != null) RenameGroupRequested(this, EventArgs.Empty);
            });
            Button done = Button("完成", 374, 584, delegate
            {
                if (DoneRequested != null) DoneRequested(this, EventArgs.Empty);
                Hide();
            });
            ((SoftButton)done).Primary = true;
            Controls.Add(addLink); Controls.Add(addFolder); Controls.Add(remove); Controls.Add(arrange); Controls.Add(_toggleOverlay); Controls.Add(rename); Controls.Add(done);

            Controls.Add(new Label { Text = "拖入文件可直接添加 · 右键图标可移除", AutoSize = true,
                ForeColor = Color.FromArgb(111, 121, 134), Location = new Point(25, 593) });
            Panel preferences = new Panel { BackColor = Color.White, Location = new Point(24, 418), Size = new Size(512, 146) };
            _transparentTaskbar = new CheckBox { Text = "任务栏透明", AutoSize = true, Location = new Point(16, 14) };
            _hideDesktopIcons = new CheckBox { Text = "隐藏系统桌面图标", AutoSize = true, Location = new Point(192, 14) };
            _startup = new CheckBox { Text = "开机自启动", AutoSize = true, Location = new Point(16, 69) };
            _startup.CheckedChanged += delegate
            {
                if (!_syncingFeatures && StartupChanged != null) StartupChanged(this, EventArgs.Empty);
            };
            _startupStatus = new Label { AutoSize = false, Size = new Size(480, 42), Location = new Point(16, 99), ForeColor = Color.FromArgb(111, 121, 134) };
            SetStartupEnabled(false);
            _taskbarStatus = new Label { AutoSize = false, Size = new Size(480, 24), Location = new Point(16, 41), ForeColor = Color.FromArgb(111, 121, 134) };
            _transparentTaskbar.CheckedChanged += delegate
            {
                if (!_syncingFeatures && TaskbarTransparencyChanged != null)
                    TaskbarTransparencyChanged(this, EventArgs.Empty);
            };
            _hideDesktopIcons.CheckedChanged += delegate
            {
                if (!_syncingFeatures && DesktopIconsVisibilityChanged != null)
                    DesktopIconsVisibilityChanged(this, EventArgs.Empty);
            };
            preferences.Controls.Add(_transparentTaskbar);
            preferences.Controls.Add(_hideDesktopIcons);
            preferences.Controls.Add(_startup);
            preferences.Controls.Add(_startupStatus);
            preferences.Controls.Add(_taskbarStatus);
            Controls.Add(preferences);
            RefreshList();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && DoneRequested != null)
                DoneRequested(this, EventArgs.Empty);
            base.OnFormClosing(e);
        }

        private Button Button(string text, int x, int y, EventHandler click)
        {
            Button b = new SoftButton { Text = text, Location = new Point(x, y), Size = new Size(162, 34) };
            b.Click += click;
            return b;
        }

        public void SetOverlayVisible(bool visible)
        {
            if (_toggleOverlay != null) _toggleOverlay.Text = visible ? "隐藏组件" : "显示组件";
        }

        public void RefreshItems()
        {
            RefreshList();
        }

        public void SetStartupEnabled(bool enabled, string status = null)
        {
            _syncingFeatures = true;
            try
            {
                _startup.Checked = enabled;
                _startupStatus.Text = status ?? "移动软件目录后自动关闭自启动；若软件已退出，下次手动打开时检查。";
            }
            finally { _syncingFeatures = false; }
        }

        public void SetFeatureStates(bool transparentTaskbar, bool hideDesktopIcons, string taskbarStatus)
        {
            _syncingFeatures = true;
            _transparentTaskbar.Checked = transparentTaskbar;
            _hideDesktopIcons.Checked = hideDesktopIcons;
            _taskbarStatus.Text = "任务栏透明状态：" + taskbarStatus;
            _syncingFeatures = false;
        }

        public void BeginAddFolder()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog { Description = "选择要显示在桌面上的文件夹" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                AddPath(dlg.SelectedPath);
            }
        }

        public void BeginAddShortcut()
        {
            AddShortcut();
        }

        private void AddShortcut()
        {
            using (OpenFileDialog dlg = new OpenFileDialog { Multiselect = true, Title = "选择快捷方式或文件", Filter = "快捷方式和文件|*.lnk;*.url;*.exe;*.bat;*.cmd;*.pdf;*.doc;*.docx;*.xlsx;*.jpg;*.png|所有文件|*.*" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (DesktopEntries.AddPaths(_settings, dlg.FileNames) > 0) Changed();
            }
        }

        private void AddPath(string path)
        {
            if (DesktopEntries.AddPaths(_settings, new[] { path }) > 0) Changed();
        }

        private void RemoveSelected()
        {
            foreach (ListViewItem row in _list.SelectedItems)
                DesktopEntries.Remove(_settings, (DesktopItem)row.Tag);
            Changed();
        }

        private void Changed()
        {
            RefreshList();
            if (SettingsChanged != null) SettingsChanged(this, EventArgs.Empty);
        }

        private void RefreshList()
        {
            if (_list == null) return;
            _list.Items.Clear();
            foreach (DesktopItem item in _settings.Items)
            {
                ListViewItem row = new ListViewItem(Path.GetFileName(item.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                if (row.Text.Length == 0) row.Text = item.Path;
                row.SubItems.Add(item.Path);
                row.Tag = item;
                _list.Items.Add(row);
            }
        }
    }
}
