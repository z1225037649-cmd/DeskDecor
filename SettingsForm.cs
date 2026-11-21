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
        private ContextMenuStrip _listMenu;
        private readonly ComboBox _groups;
        private readonly TextBox _search;
        private readonly Button _toggleOverlay;
        private readonly CheckBox _transparentTaskbar;
        private readonly CheckBox _hideDesktopIcons;
        private readonly CheckBox _startup;
        private readonly Label _startupStatus;
        private readonly Label _taskbarStatus;
        private readonly Label _persistenceStatus;
        private readonly ToolTip _settingsHelp = new ToolTip();
        private readonly NumericUpDown _iconSize;
        private readonly NumericUpDown _opacity;
        private bool _syncingFeatures;
        private bool _syncingGroups;
        public event EventHandler SettingsChanged;
        public event EventHandler ToggleOverlayRequested;
        public event EventHandler AutoArrangeRequested;
        public event EventHandler TaskbarTransparencyChanged;
        public event EventHandler DesktopIconsVisibilityChanged;
        public event EventHandler DoneRequested;
        public event EventHandler StartupChanged;
        public event EventHandler RenameGroupRequested;
        public event EventHandler GroupChanged;
        public event EventHandler AddGroupRequested;
        public event EventHandler AppearanceChanged;
        public bool StartupEnabled { get { return _startup.Checked; } }
        public int IconSize { get { return (int)_iconSize.Value; } }
        public int ItemOpacity { get { return (int)_opacity.Value; } }

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            Text = "桌面小组件";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 690);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(246, 248, 250);
            ForeColor = Color.FromArgb(37, 46, 58);
            Controls.Add(new Label { Text = "桌面小组件", AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold), Location = new Point(22, 20) });
            Controls.Add(new Label { Text = "常用文件，放在触手可及的地方。", AutoSize = true,
                ForeColor = Color.FromArgb(111, 121, 134), Location = new Point(25, 61) });
            _persistenceStatus = new Label { AutoSize = false, Size = new Size(512, 18), Location = new Point(24, 78),
                ForeColor = Color.FromArgb(170, 70, 28), AutoEllipsis = true };
            Controls.Add(_persistenceStatus);

            _search = new TextBox { Location = new Point(24, 98), Size = new Size(512, 22) };
            _search.TextChanged += delegate { RefreshList(); };
            Controls.Add(_search);
            _list = new ListView { View = View.Details, FullRowSelect = true, MultiSelect = true,
                HideSelection = false, BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White, ForeColor = ForeColor, Location = new Point(24, 126), Size = new Size(512, 178) };
            _list.Columns.Add("名称", 190);
            _list.Columns.Add("位置", 294);
            _list.DoubleClick += delegate { RenameSelectedItem(); };
            _list.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Right) return;
                ListViewItem row = _list.GetItemAt(e.X, e.Y);
                if (row == null) return;
                if (!row.Selected) { _list.SelectedItems.Clear(); row.Selected = true; }
                if (_listMenu != null) _listMenu.Dispose();
                ContextMenuStrip menu = _listMenu = new ContextMenuStrip();
                menu.Items.Add("重命名项目", null, delegate { RenameSelectedItem(); });
                ToolStripMenuItem move = new ToolStripMenuItem("移动到分组");
                foreach (GroupLayout group in _settings.Groups)
                {
                    GroupLayout destination = group;
                    if (string.Equals(destination.Id, _settings.ActiveGroupId, StringComparison.OrdinalIgnoreCase)) continue;
                    move.DropDownItems.Add(destination.DisplayTitle, null, delegate
                    {
                        foreach (ListViewItem selected in _list.SelectedItems)
                            ((DesktopItem)selected.Tag).GroupId = destination.Id;
                        Changed();
                    });
                }
                if (move.DropDownItems.Count > 0) menu.Items.Add(move);
                menu.Items.Add("移除选中入口", null, delegate { RemoveSelected(); });
                menu.Show(_list, e.Location);
            };
            Controls.Add(_list);

            _groups = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(24, 318), Size = new Size(162, 32) };
            _groups.SelectedIndexChanged += delegate
            {
                GroupChoice choice = _groups.SelectedItem as GroupChoice;
                if (!_syncingGroups && choice != null)
                {
                    _settings.ActiveGroupId = choice.Id;
                    RefreshList();
                    if (GroupChanged != null) GroupChanged(this, EventArgs.Empty);
                }
            };
            Controls.Add(_groups);
            Button addGroup = Button("＋ 新建分组", 199, 318, delegate { if (AddGroupRequested != null) AddGroupRequested(this, EventArgs.Empty); });
            Button addLink = Button("＋ 添加文件", 24, 362, delegate { AddShortcut(); });
            Button addFolder = Button("添加文件夹", 199, 362, delegate { BeginAddFolder(); });
            Button remove = Button("移除选中", 374, 362, delegate { RemoveSelected(); });
            Button arrange = Button("自动排列", 24, 406, delegate
            {
                if (AutoArrangeRequested != null) AutoArrangeRequested(this, EventArgs.Empty);
            });
            _toggleOverlay = Button("隐藏组件", 199, 406, delegate
            {
                if (ToggleOverlayRequested != null) ToggleOverlayRequested(this, EventArgs.Empty);
            });
            Button rename = Button("修改分组名称", 374, 406, delegate
            {
                if (RenameGroupRequested != null) RenameGroupRequested(this, EventArgs.Empty);
            });
            Button done = Button("完成", 374, 640, delegate
            {
                if (DoneRequested != null) DoneRequested(this, EventArgs.Empty);
                Hide();
            });
            ((SoftButton)done).Primary = true;
            Controls.Add(addGroup); Controls.Add(addLink); Controls.Add(addFolder); Controls.Add(remove); Controls.Add(arrange); Controls.Add(_toggleOverlay); Controls.Add(rename); Controls.Add(done);

            Controls.Add(new Label { Text = "拖入文件可直接添加 · 右键图标可移除", AutoSize = true,
                ForeColor = Color.FromArgb(111, 121, 134), Location = new Point(25, 649) });
            Panel preferences = new Panel { BackColor = Color.White, Location = new Point(24, 462), Size = new Size(512, 146) };
            _transparentTaskbar = new CheckBox { Text = "任务栏透明", AutoSize = true, Location = new Point(16, 14) };
            _hideDesktopIcons = new CheckBox { Text = "隐藏系统桌面图标", AutoSize = true, Location = new Point(192, 14) };
            preferences.Controls.Add(new Label { Text = "图标", AutoSize = true, Location = new Point(192, 73) });
            _iconSize = new NumericUpDown { Minimum = 16, Maximum = 32, Value = Math.Max(16, Math.Min(32, _settings.IconSize)), Location = new Point(228, 69), Width = 52 };
            preferences.Controls.Add(_iconSize);
            preferences.Controls.Add(new Label { Text = "透明度", AutoSize = true, Location = new Point(292, 73) });
            _opacity = new NumericUpDown { Minimum = 25, Maximum = 100, Increment = 5, Value = Math.Max(25, Math.Min(100, _settings.Opacity)), Location = new Point(344, 69), Width = 58 };
            preferences.Controls.Add(_opacity);
            _iconSize.ValueChanged += delegate { if (!_syncingFeatures && AppearanceChanged != null) AppearanceChanged(this, EventArgs.Empty); };
            _opacity.ValueChanged += delegate { if (!_syncingFeatures && AppearanceChanged != null) AppearanceChanged(this, EventArgs.Empty); };
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
            RefreshGroups();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && DoneRequested != null)
                DoneRequested(this, EventArgs.Empty);
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_listMenu != null) _listMenu.Dispose();
                _settingsHelp.Dispose();
            }
            base.Dispose(disposing);
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
            RefreshGroups();
        }

        public string ActiveGroupId
        {
            get
            {
                GroupChoice choice = _groups == null ? null : _groups.SelectedItem as GroupChoice;
                return choice == null ? _settings.ActiveGroupId : choice.Id;
            }
        }

        public void RefreshGroups()
        {
            if (_groups == null) return;
            string selected = _settings.ActiveGroupId;
            _syncingGroups = true;
            _groups.BeginUpdate();
            try
            {
                _groups.Items.Clear();
                foreach (GroupLayout group in _settings.Groups)
                    _groups.Items.Add(new GroupChoice(group.Id, group.DisplayTitle));
                for (int i = 0; i < _groups.Items.Count; i++)
                    if (string.Equals(((GroupChoice)_groups.Items[i]).Id, selected, StringComparison.OrdinalIgnoreCase))
                    { _groups.SelectedIndex = i; break; }
                if (_groups.SelectedIndex < 0 && _groups.Items.Count > 0) _groups.SelectedIndex = 0;
            }
            finally { _groups.EndUpdate(); _syncingGroups = false; }
        }

        private sealed class GroupChoice
        {
            public readonly string Id;
            private readonly string _title;
            public GroupChoice(string id, string title) { Id = id; _title = title; }
            public override string ToString() { return _title; }
        }

        public void SetPersistenceStatus(string status)
        {
            if (_persistenceStatus == null) return;
            _persistenceStatus.Text = status ?? string.Empty;
            _persistenceStatus.Visible = !string.IsNullOrEmpty(status);
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

        public void SetAppearance(int iconSize, int opacity)
        {
            _syncingFeatures = true;
            try
            {
                _iconSize.Value = Math.Max(_iconSize.Minimum, Math.Min(_iconSize.Maximum, iconSize));
                _opacity.Value = Math.Max(_opacity.Minimum, Math.Min(_opacity.Maximum, opacity));
            }
            finally { _syncingFeatures = false; }
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
                if (DesktopEntries.AddPaths(_settings, dlg.FileNames, _settings.ActiveGroupId) > 0) Changed();
            }
        }

        private void AddPath(string path)
        {
            if (DesktopEntries.AddPaths(_settings, new[] { path }, _settings.ActiveGroupId) > 0) Changed();
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
                if (!string.Equals(item.GroupId, _settings.ActiveGroupId, StringComparison.OrdinalIgnoreCase)) continue;
                string name = string.IsNullOrWhiteSpace(item.Label) ? Path.GetFileName(item.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) : item.Label;
                string query = _search == null ? string.Empty : _search.Text.Trim();
                if (query.Length > 0 && name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0 &&
                    item.Path.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                ListViewItem row = new ListViewItem(name);
                if (row.Text.Length == 0) row.Text = item.Path;
                row.SubItems.Add(item.Path);
                row.Tag = item;
                _list.Items.Add(row);
            }
        }

        private void RenameSelectedItem()
        {
            if (_list.SelectedItems.Count != 1) return;
            DesktopItem item = (DesktopItem)_list.SelectedItems[0].Tag;
            string current = string.IsNullOrWhiteSpace(item.Label) ? _list.SelectedItems[0].Text : item.Label;
            using (Form dialog = new Form { Text = "重命名项目", ClientSize = new Size(360, 105),
                FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false })
            {
                TextBox input = new TextBox { Text = current, Location = new Point(12, 14), Width = 336 };
                Button ok = new Button { Text = "保存", DialogResult = DialogResult.OK, Location = new Point(188, 56), Width = 76 };
                Button cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(272, 56), Width = 76 };
                dialog.Controls.Add(input); dialog.Controls.Add(ok); dialog.Controls.Add(cancel);
                dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(input.Text))
                {
                    item.Label = input.Text.Trim();
                    Changed();
                }
            }
        }
    }
}
