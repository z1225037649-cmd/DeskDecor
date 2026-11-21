using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace DeskDecor
{
    internal sealed class OverlayForm : Form
    {
        private int Scale(int value) { return MonitorDpi.Scale(value, ActiveGroup.Dpi); }
        private int TileWidth { get { return Scale(76); } }
        private int TileHeight { get { return Scale(66); } }
        private int ColumnPitch { get { return Scale(82); } }
        private int RowPitch { get { return Scale(76); } }
        private int GroupPadding { get { return Scale(18); } }
        private int ResizeHandleSize { get { return Scale(22); } }
        private int GroupHeaderHeight { get { return Scale(34); } }
        private int CollapsedHeight { get { return Scale(42); } }
        private readonly LayeredSurface _surface = new LayeredSurface();
        private readonly ShellIconCache _icons = new ShellIconCache();
        private readonly Font _labelFont = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly ToolTip _tooltip = new ToolTip { InitialDelay = 650, ReshowDelay = 250, AutoPopDelay = 6000, ShowAlways = true };
        private DesktopItem _hoveredItem;
        private bool _groupHovered;
        private bool _resizeHovered;
        private bool _collapseHovered;
        private bool _titleHovered;
        private InlineTitleEditor _titleEditor;
        private bool _collapsePressed;
        private bool _externalDropHover;
        private bool _suppressContextMenu;
        private readonly ContextMenuStrip _itemMenu = new ContextMenuStrip();
        private readonly ContextMenuStrip _headerMenu = new ContextMenuStrip();
        private DesktopItem _contextItem;
        private bool _diagnosticsWritten;
        private bool _movingGroup;
        private bool _resizingGroup;
        private readonly ItemReorderGesture _reorder = new ItemReorderGesture();
        private readonly Timer _dragCancelTimer = new Timer { Interval = 40 };
        private Point _dragStart;
        private GroupLayout _groupStart;
        private readonly System.Collections.Generic.List<Point> _itemStart = new System.Collections.Generic.List<Point>();
        private Rectangle _lastDesktopBounds;
        private string _selectedGroupId;
        private GroupLayout _renderingGroup;

        public AppSettings Settings { get; private set; }
        public bool LayoutWasRecovered { get; private set; }
        private GroupLayout ActiveGroup
        {
            get
            {
                if (_renderingGroup != null) return _renderingGroup;
                if (Settings.Groups != null)
                    foreach (GroupLayout group in Settings.Groups)
                        if (group != null && string.Equals(group.Id, _selectedGroupId, StringComparison.OrdinalIgnoreCase))
                            return group;
                return Settings.Group;
            }
        }
        public event EventHandler<DesktopItem> ItemActivated;
        public event EventHandler ItemMoved;
        public event EventHandler ItemsChanged;
        public event EventHandler ActiveGroupChanged;
        public event EventHandler GroupTitleChanged;

        public OverlayForm(AppSettings settings)
        {
            Settings = settings;
            Settings.Normalize();
            _selectedGroupId = Settings.ActiveGroupId;
            Text = "DeskDecor.DesktopSurface";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = false;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Bounds = SystemInformation.VirtualScreen;
            _lastDesktopBounds = Bounds;
            AllowDrop = true;
            if (!Settings.DesktopInitialized && Settings.Items.Count == 0) AddDefaultDesktopItems();
            Settings.DesktopInitialized = true;
            foreach (GroupLayout group in Settings.Groups) EnsureGroupBounds(group);
            LayoutWasRecovered = RestoreLayoutToVisibleMonitor();
            foreach (GroupLayout group in Settings.Groups) LayoutItemsInGroup(group);
            _itemMenu.Font = new Font("Microsoft YaHei UI", 9F);
            _itemMenu.Items.Add("移除图标（保留原文件）", null, delegate
            {
                DesktopItem target = _contextItem;
                _contextItem = null;
                if (DesktopEntries.Remove(Settings, target)) NotifyItemsChanged();
            });
            _itemMenu.Closed += delegate { _tooltip.Active = true; };
            _headerMenu.Items.Add("修改分组名称", null, delegate { RenameGroup(); });
            _dragCancelTimer.Tick += delegate
            {
                // The desktop surface does not take keyboard focus. Check Escape
                // only during this captured gesture, without a global hotkey.
                if (_reorder.Source != null && (Native.GetAsyncKeyState(0x1B) & 0x8000) != 0)
                    CancelItemDrag();
            };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        private void AddDefaultDesktopItems()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            AddItemsFrom(desktop);
            if (!string.Equals(desktop, publicDesktop, StringComparison.OrdinalIgnoreCase)) AddItemsFrom(publicDesktop);
        }

        private void AddItemsFrom(string directory)
        {
            try
            {
                foreach (string path in Directory.GetFileSystemEntries(directory))
                {
                    if (IsDesktopMetadata(path)) continue;
                    if (Settings.Items.Exists(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
                    int n = Settings.Items.Count;
                    Settings.Items.Add(new DesktopItem(path, 24 + (n / 10) * 66, 24 + (n % 10) * 74)
                        { GroupId = Settings.Group.Id });
                }
            }
            catch { }
        }

        private void EnsureGroupBounds()
        {
            EnsureGroupBounds(ActiveGroup);
        }

        private void EnsureGroupBounds(GroupLayout group)
        {
            string previous = _selectedGroupId;
            _selectedGroupId = group.Id;
            if (ActiveGroup.Width > 0 && ActiveGroup.Height > 0)
            {
                _selectedGroupId = previous;
                return;
            }
            if (DisplayItems().Count == 0)
            {
                ActiveGroup.X = 20;
                ActiveGroup.Y = 20;
                ActiveGroup.Width = 300;
                ActiveGroup.Height = 180;
                _selectedGroupId = previous;
                return;
            }

            int left = int.MaxValue, top = int.MaxValue, right = 0, bottom = 0;
            foreach (DesktopItem item in DisplayItems())
            {
                if (IsDesktopMetadata(item.Path)) continue;
                left = Math.Min(left, item.X);
                top = Math.Min(top, item.Y);
                right = Math.Max(right, item.X + TileWidth);
                bottom = Math.Max(bottom, item.Y + TileHeight);
            }
            if (left == int.MaxValue)
            {
                ActiveGroup.X = 20; ActiveGroup.Y = 20;
                ActiveGroup.Width = 300; ActiveGroup.Height = 180;
                _selectedGroupId = previous;
                return;
            }
            ActiveGroup.X = Math.Max(8, left - 12);
            ActiveGroup.Y = Math.Max(8, top - GroupHeaderHeight - 8);
            ActiveGroup.Width = right - ActiveGroup.X + 12;
            ActiveGroup.Height = bottom - ActiveGroup.Y + 12;
            _selectedGroupId = previous;
        }

        public void InvalidateSurface() { Redraw(); }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DesktopFeatures.AttachOverlayToDesktop(Handle, Bounds);
            Redraw();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            DesktopFeatures.AttachOverlayToDesktop(Handle, Bounds);
            Redraw();
        }

        public void EnsureDesktopAttached()
        {
            if (IsHandleCreated && Visible)
                DesktopFeatures.AttachOverlayToDesktop(Handle, SystemInformation.VirtualScreen);
        }

        public void RefreshDesktopLayout()
        {
            if (IsDisposed || !IsHandleCreated) return;
            Rectangle desktop = SystemInformation.VirtualScreen;
            bool desktopChanged = desktop != _lastDesktopBounds;
            if (desktopChanged)
            {
                _lastDesktopBounds = desktop;
                Bounds = desktop;
            }
            bool recovered = RestoreLayoutToVisibleMonitor();
            if (recovered)
            {
                foreach (GroupLayout group in Settings.Groups) LayoutItemsInGroup(group);
                if (ItemMoved != null) ItemMoved(this, EventArgs.Empty);
            }
            if (desktopChanged || recovered)
            {
                EnsureDesktopAttached();
                Redraw();
            }
        }

        private bool RestoreLayoutToVisibleMonitor()
        {
            Rectangle virtualScreen = SystemInformation.VirtualScreen;
            var areas = new System.Collections.Generic.List<Rectangle>();
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle area = screen.WorkingArea;
                area.Offset(-virtualScreen.Left, -virtualScreen.Top);
                areas.Add(area);
            }
            bool recovered = false;
            foreach (GroupLayout group in Settings.Groups)
            {
                var groupItems = Settings.Items.FindAll(item => !IsDesktopMetadata(item.Path) &&
                    string.Equals(item.GroupId, group.Id, StringComparison.OrdinalIgnoreCase));
                int dpi = MonitorDpi.AtPoint(new Point(virtualScreen.Left + group.X + Math.Max(1, group.Width) / 2,
                    virtualScreen.Top + group.Y + MonitorDpi.Scale(34, group.Dpi) / 2));
                if (MonitorDpi.UpdateGroupDpi(group, groupItems, dpi, MonitorDpi.Scale(150, dpi), MonitorDpi.Scale(100, dpi)))
                    recovered = true;
                if (DesktopLayoutRecovery.Restore(group, groupItems, areas)) recovered = true;
            }
            if (recovered) LayoutWasRecovered = true;
            return recovered;
        }

        public void SetActiveGroup(string id)
        {
            if (!Settings.Groups.Exists(group => string.Equals(group.Id, id, StringComparison.OrdinalIgnoreCase))) return;
            if (string.Equals(_selectedGroupId, id, StringComparison.OrdinalIgnoreCase))
            {
                Settings.ActiveGroupId = id;
                return;
            }
            _selectedGroupId = id;
            Settings.ActiveGroupId = id;
            Redraw();
            if (ActiveGroupChanged != null) ActiveGroupChanged(this, EventArgs.Empty);
        }

        public void AddGroup(string title)
        {
            var group = new GroupLayout
            {
                Title = title,
                X = 28 + (Settings.Groups.Count % 5) * 320,
                Y = 28 + (Settings.Groups.Count / 5) * 240,
                Width = 300,
                Height = 180
            };
            Settings.Groups.Add(group);
            SetActiveGroup(group.Id);
        }

        private bool ActivateGroupAt(Point point)
        {
            for (int i = Settings.Groups.Count - 1; i >= 0; i--)
            {
                GroupLayout group = Settings.Groups[i];
                if (!new Rectangle(group.X, group.Y, group.Width,
                    group.Collapsed ? MonitorDpi.Scale(42, group.Dpi) : group.Height).Contains(point)) continue;
                if (string.Equals(group.Id, _selectedGroupId, StringComparison.OrdinalIgnoreCase)) return false;
                SetActiveGroup(group.Id);
                return true;
            }
            return false;
        }

        public void MoveItemToGroup(DesktopItem item, string groupId)
        {
            if (item == null || !Settings.Groups.Exists(group => string.Equals(group.Id, groupId, StringComparison.OrdinalIgnoreCase))) return;
            if (string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase)) return;
            item.GroupId = groupId;
            RefreshItemsLayout();
            if (ItemMoved != null) ItemMoved(this, EventArgs.Empty);
            if (ItemsChanged != null) ItemsChanged(this, EventArgs.Empty);
        }

        public void ShowDesktopSurface()
        {
            if (!Visible) Show();
            EnsureDesktopAttached();
            Redraw();
        }

        private void Redraw()
        {
            if (!IsHandleCreated || IsDisposed) return;
            Bitmap bitmap = _surface.Begin(Width, Height);
            if (bitmap == null) return;
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                PaintSurface(g);
            }
            // This is a top-level popup, so the bitmap uses screen coordinates.
            Rectangle desktop = SystemInformation.VirtualScreen;
            Point screenOrigin = desktop.Location;
            bool pushed = _surface.Push(Handle, screenOrigin.X, screenOrigin.Y);
            if (pushed && !_diagnosticsWritten)
            {
                try
                {
                    bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskDecor.surface.png"),
                        System.Drawing.Imaging.ImageFormat.Png);
                    ShellDiagnostics.Write();
                    _diagnosticsWritten = true;
                }
                catch { }
            }
            if (!pushed)
            {
                try
                {
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskDecor.debug.log"),
                        DateTime.Now.ToString("s") + " UpdateLayeredWindow failed: " + _surface.LastError +
                        " hwnd=" + Handle + " origin=" + screenOrigin + Environment.NewLine);
                }
                catch { }
            }
        }

        internal void PaintSurface(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            try
            {
                foreach (GroupLayout group in Settings.Groups)
                {
                    _renderingGroup = group;
                    DrawGroupFrame(g);
                    DrawGroupHeader(g);
                    if (group.Collapsed) continue;
                    foreach (DesktopItem item in DisplayItems())
                        if (!IsDesktopMetadata(item.Path)) DrawItem(g, item);
                    if (_reorder.Dragging && _reorder.Source != null &&
                        string.Equals(group.Id, _selectedGroupId, StringComparison.OrdinalIgnoreCase))
                    {
                        DrawDropIndicator(g);
                        DesktopItem source = _reorder.Source;
                        Rectangle ghost = new Rectangle(_reorder.Pointer.X - (_reorder.PressPoint.X - source.X),
                            _reorder.Pointer.Y - (_reorder.PressPoint.Y - source.Y), TileWidth, TileHeight);
                        DrawItem(g, source, ghost, 195, false);
                    }
                }
            }
            finally { _renderingGroup = null; }
        }

        private static bool IsDesktopMetadata(string path)
        {
            return DesktopEntries.IsMetadata(path);
        }

        private static string ItemLabel(DesktopItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.Label)) return item.Label;
            string label = Path.GetFileName(item.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string extension = Path.GetExtension(label);
            if (string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".url", StringComparison.OrdinalIgnoreCase))
                label = Path.GetFileNameWithoutExtension(label);
            return string.IsNullOrEmpty(label) ? item.Path : label;
        }

        private void DrawItem(Graphics g, DesktopItem item)
        {
            DrawItem(g, item, TileBounds(item), _reorder.Dragging && item == _reorder.Source ? 85 : 255,
                !_reorder.Dragging && item == _hoveredItem);
        }

        private void DrawItem(Graphics g, DesktopItem item, Rectangle tile, int opacity, bool hovered)
        {
            int iconSize = Scale(Math.Max(16, Math.Min(32, Settings.IconSize <= 0 ? 24 : Settings.IconSize)));
            opacity = opacity * Math.Max(25, Math.Min(100, Settings.Opacity <= 0 ? 100 : Settings.Opacity)) / 100;
            if (hovered)
            {
                using (GraphicsPath hover = RoundedRectangle(new RectangleF(tile.X, tile.Y, tile.Width, tile.Height), Scale(10)))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(22, Color.White)))
                using (Pen line = new Pen(Color.FromArgb(40, Color.White)))
                { g.FillPath(fill, hover); g.DrawPath(line, hover); }
            }
            Rectangle iconRect = new Rectangle(tile.Left + (TileWidth - iconSize) / 2, tile.Top + Scale(6), iconSize, iconSize);
            Bitmap icon = _icons.GetOutlined(item.Path, iconSize);
            iconRect.Inflate(2, 2);
            using (ImageAttributes attributes = new ImageAttributes())
            {
                ColorMatrix matrix = new ColorMatrix();
                matrix.Matrix33 = opacity / 255F;
                attributes.SetColorMatrix(matrix);
                g.DrawImage(icon, iconRect, 0, 0, icon.Width, icon.Height, GraphicsUnit.Pixel, attributes);
            }

            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap })
            {
                int labelTop = Math.Max(Scale(37), iconSize + Scale(12));
                RectangleF labelBounds = new RectangleF(tile.Left + Scale(1), tile.Top + labelTop, TileWidth - Scale(2), Scale(21));
                string label = ItemLabel(item);
                DrawOutlinedLabel(g, label, labelBounds, sf, opacity);
            }
        }

        private void DrawOutlinedLabel(Graphics g, string text, RectangleF bounds, StringFormat format, int opacity)
        {
            using (var path = new GraphicsPath())
            using (var outline = new Pen(Color.FromArgb(195 * opacity / 255, 0, 0, 0), 1.5F))
            using (var shadow = new SolidBrush(Color.FromArgb(90 * opacity / 255, 0, 0, 0)))
            using (var fill = new SolidBrush(Color.FromArgb(250 * opacity / 255, Color.White)))
            using (var offset = new Matrix())
            {
                path.AddString(text, _labelFont.FontFamily, (int)_labelFont.Style, Scale((int)_labelFont.Size),
                    bounds, format);
                outline.LineJoin = LineJoin.Round;
                offset.Translate(0, 1.4F);
                using (var shadowPath = (GraphicsPath)path.Clone())
                {
                    shadowPath.Transform(offset);
                    g.FillPath(shadow, shadowPath);
                }
                g.DrawPath(outline, path);
                g.FillPath(fill, path);
            }
        }

        private void DrawDropIndicator(Graphics g)
        {
            var items = DisplayItems();
            int slot = _reorder.DropIndex;
            int from = items.IndexOf(_reorder.Source);
            if (slot < 0 || items.Count == 0 || slot == from || slot == from + 1) return;
            Rectangle tile = TileBounds(items[Math.Min(slot, items.Count - 1)]);
            int x = slot < items.Count ? tile.Left - Scale(3) : tile.Right + Scale(3);
            using (Pen shadow = new Pen(Color.FromArgb(150, 10, 18, 28), 4F))
            using (Pen line = new Pen(Color.FromArgb(240, 235, 247, 255), 2F))
            {
                shadow.StartCap = shadow.EndCap = line.StartCap = line.EndCap = LineCap.Round;
                g.DrawLine(shadow, x, tile.Top + Scale(6), x, tile.Bottom - Scale(6));
                g.DrawLine(line, x, tile.Top + Scale(6), x, tile.Bottom - Scale(6));
            }
        }

        private void DrawGroupFrame(Graphics g)
        {
            Rectangle frame = GroupBounds();
            frame.Width--; frame.Height--;
            // A nonzero alpha keeps transparent interior pixels hit-testable while remaining visually clear.
            using (GraphicsPath outline = RoundedRectangle(frame, Scale(16)))
            using (SolidBrush hitSurface = new SolidBrush(Color.FromArgb(1, 255, 255, 255)))
            using (LinearGradientBrush borderBrush = new LinearGradientBrush(frame,
                Color.FromArgb(_externalDropHover ? 220 : _groupHovered ? 125 : 58, Color.White),
                Color.FromArgb(_externalDropHover ? 160 : _groupHovered ? 65 : 25, Color.White), LinearGradientMode.Vertical))
            using (Pen border = new Pen(borderBrush, Scale(1)))
            {
                g.FillPath(hitSurface, outline);
                g.DrawPath(border, outline);
            }
            if (!ActiveGroup.Collapsed && (_groupHovered || _resizingGroup))
            {
                using (Pen corner = new Pen(Color.FromArgb(_resizeHovered || _resizingGroup ? 230 : 115, Color.White), 2f))
                {
                    corner.StartCap = corner.EndCap = LineCap.Round;
                    g.DrawArc(corner, frame.Right - 20, frame.Bottom - 20, 12, 12, 0, 90);
                }
            }
        }

        private Rectangle CollapseButtonBounds()
        {
            return new Rectangle(ActiveGroup.X + ActiveGroup.Width - Scale(72), ActiveGroup.Y + Scale(7), Scale(60), Scale(26));
        }

        private Rectangle TitleBounds()
        {
            return new Rectangle(ActiveGroup.X + Scale(18), ActiveGroup.Y + Scale(6),
                Math.Max(1, ActiveGroup.Width - Scale(104)), Scale(26));
        }

        public void RenameGroup()
        {
            if (_titleEditor != null && !_titleEditor.IsDisposed)
            {
                _titleEditor.Activate();
                return;
            }
            CancelItemDrag();
            _movingGroup = _resizingGroup = _collapsePressed = false;
            Capture = false;
            _tooltip.Active = false;
            Rectangle bounds = TitleBounds();
            bounds.Location = PointToScreen(bounds.Location);
            var editor = new InlineTitleEditor(ActiveGroup.DisplayTitle, bounds);
            _titleEditor = editor;
            editor.FormClosed += delegate
            {
                _titleEditor = null;
                _tooltip.Active = true;
                bool changed = editor.SaveChanges && ActiveGroup.Title != editor.Value;
                if (changed) ActiveGroup.Title = editor.Value;
                if (!IsDisposed && !Disposing)
                {
                    EnsureDesktopAttached();
                    Redraw();
                    if (changed && ItemMoved != null) ItemMoved(this, EventArgs.Empty);
                    if (changed && GroupTitleChanged != null) GroupTitleChanged(this, EventArgs.Empty);
                }
            };
            Redraw();
            // A keyboard-active window owned by this surface also raises the
            // surface and its Explorer owner. Keep the tiny editor independent.
            editor.Show();
            EnsureDesktopAttached();
        }

        private void DrawGroupHeader(Graphics g)
        {
            Rectangle button = CollapseButtonBounds();
            if (_collapseHovered)
            {
                using (GraphicsPath path = RoundedRectangle(button, Scale(8)))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(22, Color.White)))
                    g.FillPath(fill, path);
            }
            using (StringFormat format = new StringFormat { LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                string title = (_externalDropHover ? "松开添加" : ActiveGroup.DisplayTitle) + " · " + DisplayItems().Count;
                RectangleF label = TitleBounds();
                if (_titleEditor == null)
                {
                    DrawOutlinedLabel(g, title, label, format, 255);
                }
                DrawOutlinedLabel(g, ActiveGroup.Collapsed ? "展开" : "收起",
                    new RectangleF(button.Left + 9, button.Top, 32, button.Height), format, 255);
            }
            using (Pen pen = new Pen(Color.FromArgb(225, Color.White), 1.4F))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                int x = button.Right - Scale(14), y = button.Top + Scale(13);
                int dy = ActiveGroup.Collapsed ? -Scale(2) : Scale(2);
                g.DrawLines(pen, new[] { new Point(x - Scale(4), y + dy), new Point(x, y - dy), new Point(x + Scale(4), y + dy) });
            }
        }

        private void ToggleCollapsed()
        {
            CancelItemDrag();
            ActiveGroup.Collapsed = !ActiveGroup.Collapsed;
            UpdateHover(new Point(int.MinValue, int.MinValue));
            Redraw();
            if (ItemMoved != null) ItemMoved(this, EventArgs.Empty);
        }

        public void RefreshItemsLayout()
        {
            CancelItemDrag();
            foreach (GroupLayout group in Settings.Groups) LayoutItemsInGroup(group);
            Redraw();
        }

        private void NotifyItemsChanged()
        {
            RefreshItemsLayout();
            if (ItemMoved != null) ItemMoved(this, EventArgs.Empty);
            if (ItemsChanged != null) ItemsChanged(this, EventArgs.Empty);
        }

        private void SetExternalDropHover(bool value)
        {
            if (_externalDropHover == value) return;
            _externalDropHover = value;
            Redraw();
        }

        private void UpdateExternalDrag(DragEventArgs e)
        {
            Point p = PointToClient(new Point(e.X, e.Y));
            ActivateGroupAt(p);
            bool accept = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop) && GroupBounds().Contains(p);
            e.Effect = !accept ? DragDropEffects.None :
                (e.AllowedEffect & DragDropEffects.Link) != 0 ? DragDropEffects.Link :
                (e.AllowedEffect & DragDropEffects.Copy) != 0 ? DragDropEffects.Copy : DragDropEffects.None;
            SetExternalDropHover(e.Effect != DragDropEffects.None);
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            _tooltip.Active = false;
            UpdateExternalDrag(e);
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            UpdateExternalDrag(e);
        }

        protected override void OnDragLeave(EventArgs e)
        {
            base.OnDragLeave(e);
            _tooltip.Active = true;
            SetExternalDropHover(false);
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            UpdateExternalDrag(e);
            try
            {
                if (e.Effect == DragDropEffects.None) return;
                string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (DesktopEntries.AddPaths(Settings, paths, ActiveGroup.Id) == 0) return;
                ActiveGroup.Collapsed = false;
                NotifyItemsChanged();
            }
            finally
            {
                _tooltip.Active = true;
                SetExternalDropHover(false);
            }
        }

        private static GraphicsPath RoundedRectangle(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Rectangle TileBounds(DesktopItem item)
        {
            return new Rectangle(item.X, item.Y, TileWidth, TileHeight);
        }

        public void ArrangeItems()
        {
            EnsureGroupBounds();
            LayoutItemsInGroup();
            Redraw();
            if (ItemMoved != null) ItemMoved(this, EventArgs.Empty);
        }

        private void LayoutItemsInGroup()
        {
            LayoutItemsInGroup(ActiveGroup);
        }

        private void LayoutItemsInGroup(GroupLayout group)
        {
            string selectedId = _selectedGroupId;
            _selectedGroupId = group.Id;
            int columns = ColumnCount();
            int inset = Math.Max(GroupPadding, (ActiveGroup.Width - (columns - 1) * ColumnPitch - TileWidth) / 2);
            int n = 0;
            foreach (DesktopItem item in Settings.Items)
            {
                if (IsDesktopMetadata(item.Path) || !string.Equals(item.GroupId, group.Id, StringComparison.OrdinalIgnoreCase)) continue;
                item.X = ActiveGroup.X + inset + (n % columns) * ColumnPitch;
                item.Y = ActiveGroup.Y + GroupHeaderHeight + GroupPadding + (n / columns) * RowPitch;
                n++;
            }
            int rows = (n + columns - 1) / columns;
            ActiveGroup.Height = Math.Max(ActiveGroup.Height, GroupHeaderHeight + GroupPadding * 2 + Math.Max(0, rows - 1) * RowPitch + TileHeight);
            _selectedGroupId = selectedId;
        }

        private int ColumnCount()
        {
            return Math.Max(1, (ActiveGroup.Width - GroupPadding * 2 + ColumnPitch - TileWidth) / ColumnPitch);
        }

        private System.Collections.Generic.List<DesktopItem> DisplayItems()
        {
            return Settings.Items.FindAll(item => !IsDesktopMetadata(item.Path) &&
                string.Equals(item.GroupId, ActiveGroup.Id, StringComparison.OrdinalIgnoreCase));
        }

        private void UpdateItemDrag(Point p)
        {
            _reorder.Update(p, DisplayItems(), GroupBounds(), ColumnCount(),
                new Size(ColumnPitch, RowPitch), new Size(TileWidth, TileHeight));
            if (_reorder.Dragging)
            {
                _tooltip.Active = false;
                Cursor = _reorder.DropIndex < 0 ? Cursors.No : Cursors.Default;
                Redraw();
            }
        }

        private void CancelItemDrag()
        {
            if (_reorder.Source == null) return;
            _reorder.Cancel();
            _dragCancelTimer.Stop();
            _tooltip.Active = true;
            Capture = false;
            Cursor = Cursors.Default;
            Redraw();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                _suppressContextMenu = _reorder.Source != null;
                Point screen = PointToScreen(e.Location);
                screen.Offset(-Bounds.Left, -Bounds.Top);
                ActivateGroupAt(screen);
                CancelItemDrag();
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            Point p = PointToScreen(e.Location);
            p.Offset(-Bounds.Left, -Bounds.Top);
            ActivateGroupAt(p);
            if (CollapseButtonBounds().Contains(p))
            {
                _collapsePressed = true;
                Capture = true;
                return;
            }
            if (e.Clicks >= 2 && TitleBounds().Contains(p))
            {
                RenameGroup();
                return;
            }

            for (int i = Settings.Items.Count - 1; !ActiveGroup.Collapsed && i >= 0; i--)
            {
                DesktopItem item = Settings.Items[i];
                if (!string.Equals(item.GroupId, ActiveGroup.Id, StringComparison.OrdinalIgnoreCase)) continue;
                if (IsDesktopMetadata(item.Path)) continue;
                if (!TileBounds(item).Contains(p)) continue;
                if (e.Clicks >= 2)
                {
                    if (ItemActivated != null) ItemActivated(this, item);
                    return;
                }
                _reorder.Begin(item, p, SystemInformation.DragSize);
                _dragCancelTimer.Start();
                Capture = true;
                return;
            }

            Rectangle handle = ResizeHandleBounds();
            if (!ActiveGroup.Collapsed && handle.Contains(p))
            {
                BeginGroupGesture(p, false, true);
                return;
            }

            Rectangle group = GroupBounds();
            if (group.Contains(p))
            {
                BeginGroupGesture(p, true, false);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = PointToScreen(e.Location);
            p.Offset(-Bounds.Left, -Bounds.Top);
            if (_reorder.Source != null)
            {
                if (e.Button != MouseButtons.Left) return;
                UpdateItemDrag(p);
                return;
            }
            UpdateHover(p);
            if (_collapsePressed) return;
            if (!_movingGroup && !_resizingGroup)
            {
                bool overItem = false;
                foreach (DesktopItem item in DisplayItems())
                    if (!ActiveGroup.Collapsed && !IsDesktopMetadata(item.Path) && TileBounds(item).Contains(p)) { overItem = true; break; }
                Cursor = CollapseButtonBounds().Contains(p) ? Cursors.Hand
                    : !ActiveGroup.Collapsed && ResizeHandleBounds().Contains(p) ? Cursors.SizeNWSE
                    : overItem ? Cursors.Default : GroupBounds().Contains(p)
                        ? Cursors.SizeAll : Cursors.Default;
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            int dx = p.X - _dragStart.X;
            int dy = p.Y - _dragStart.Y;
            if (_movingGroup)
            {
                ActiveGroup.X = Math.Max(0, _groupStart.X + dx);
                ActiveGroup.Y = Math.Max(0, _groupStart.Y + dy);
                System.Collections.Generic.List<DesktopItem> groupItems = DisplayItems();
                for (int i = 0; i < groupItems.Count && i < _itemStart.Count; i++)
                {
                    groupItems[i].X = Math.Max(0, _itemStart[i].X + dx);
                    groupItems[i].Y = Math.Max(0, _itemStart[i].Y + dy);
                }
            }
            else
            {
                int minHeight = MinimumGroupHeight();
                int minWidth = Scale(150);
                Rectangle desktop = SystemInformation.VirtualScreen;
                int maxWidth = Math.Max(minWidth, desktop.Width - ActiveGroup.X - Scale(8));
                int maxHeight = Math.Max(minHeight, desktop.Height - ActiveGroup.Y - Scale(8));
                ActiveGroup.Width = Math.Max(minWidth, Math.Min(maxWidth, _groupStart.Width + dx));
                int requestedHeight = Math.Max(100, Math.Min(maxHeight, _groupStart.Height + dy));
                ActiveGroup.Height = requestedHeight;
                LayoutItemsInGroup();
            }
            Redraw();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                Point screen = PointToScreen(e.Location);
                screen.Offset(-Bounds.Left, -Bounds.Top);
                ActivateGroupAt(screen);
                if (!_suppressContextMenu && TitleBounds().Contains(e.Location))
                {
                    _headerMenu.Show(this, e.Location);
                    return;
                }
                if (!_suppressContextMenu && !ActiveGroup.Collapsed)
                {
                    foreach (DesktopItem item in DisplayItems())
                    {
                        if (!TileBounds(item).Contains(e.Location)) continue;
                        _contextItem = item;
                        _tooltip.Active = false;
                        _itemMenu.Show(this, e.Location);
                        break;
                    }
                }
                _suppressContextMenu = false;
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (_collapsePressed)
            {
                bool toggle = CollapseButtonBounds().Contains(e.Location);
                _collapsePressed = false;
                Capture = false;
                if (toggle) ToggleCollapsed();
                return;
            }
            if (_reorder.Source != null)
            {
                Point p = e.Location;
                // Recompute the final slot from the actual release, not a target
                // crossed earlier while the button was held.
                UpdateItemDrag(p);
                bool changed = _reorder.Commit(Settings.Items, DisplayItems());
                _dragCancelTimer.Stop();
                _tooltip.Active = true;
                Capture = false;
                Cursor = Cursors.Default;
                if (changed) LayoutItemsInGroup();
                UpdateHover(p);
                Redraw();
                if (changed && ItemMoved != null) ItemMoved(this, EventArgs.Empty);
                return;
            }
            if (!_movingGroup && !_resizingGroup) return;
            _movingGroup = false;
            _resizingGroup = false;
            Capture = false;
            if (ItemMoved != null) ItemMoved(this, EventArgs.Empty);
        }

        private void UpdateHover(Point p)
        {
            if (!_movingGroup && !_resizingGroup && !_collapsePressed) ActivateGroupAt(p);
            DesktopItem hovered = null;
            foreach (DesktopItem item in DisplayItems())
                if (!ActiveGroup.Collapsed && !IsDesktopMetadata(item.Path) && TileBounds(item).Contains(p)) { hovered = item; break; }
            bool group = GroupBounds().Contains(p);
            bool resize = !ActiveGroup.Collapsed && ResizeHandleBounds().Contains(p);
            bool collapse = CollapseButtonBounds().Contains(p);
            bool title = TitleBounds().Contains(p);
            if (hovered == _hoveredItem && group == _groupHovered && resize == _resizeHovered && collapse == _collapseHovered && title == _titleHovered) return;
            if (hovered != _hoveredItem || collapse != _collapseHovered || title != _titleHovered)
                _tooltip.SetToolTip(this, collapse ? (ActiveGroup.Collapsed ? "展开内容" : "收起内容") :
                    title ? "双击修改分组名称" :
                    hovered == null ? null : ItemLabel(hovered) + "\n" + hovered.Path);
            _hoveredItem = hovered;
            _groupHovered = group;
            _resizeHovered = resize;
            _collapseHovered = collapse;
            _titleHovered = title;
            Redraw();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!Capture) UpdateHover(new Point(int.MinValue, int.MinValue));
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture)
            {
                _collapsePressed = false;
                CancelItemDrag();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible)
            {
                if (_titleEditor != null) _titleEditor.Finish(true);
                CancelItemDrag();
            }
        }

        private void BeginGroupGesture(Point p, bool move, bool resize)
        {
            _movingGroup = move;
            _resizingGroup = resize;
            _dragStart = p;
            _groupStart = new GroupLayout
            {
                X = ActiveGroup.X,
                Y = ActiveGroup.Y,
                Width = ActiveGroup.Width,
                Height = ActiveGroup.Height
            };
            _itemStart.Clear();
            foreach (DesktopItem item in DisplayItems()) _itemStart.Add(new Point(item.X, item.Y));
            Capture = true;
        }

        private Rectangle GroupBounds()
        {
            return new Rectangle(ActiveGroup.X, ActiveGroup.Y,
                Math.Max(1, ActiveGroup.Width), ActiveGroup.Collapsed ? CollapsedHeight : Math.Max(1, ActiveGroup.Height));
        }

        private Rectangle ResizeHandleBounds()
        {
            Rectangle group = GroupBounds();
            return new Rectangle(group.Right - ResizeHandleSize, group.Bottom - ResizeHandleSize,
                ResizeHandleSize, ResizeHandleSize);
        }

        private int MinimumGroupHeight()
        {
            int height = GroupHeaderHeight + Scale(40);
            foreach (DesktopItem item in DisplayItems())
            {
                if (IsDesktopMetadata(item.Path)) continue;
                height = Math.Max(height, item.Y - ActiveGroup.Y + TileHeight + Scale(10));
            }
            return height;
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_DISPLAYCHANGE = 0x007E;
            const int WM_DPICHANGED = 0x02E0;
            if (m.Msg == WM_DISPLAYCHANGE || m.Msg == WM_DPICHANGED)
            {
                base.WndProc(ref m);
                _lastDesktopBounds = Rectangle.Empty;
                RefreshDesktopLayout();
                return;
            }
            if (m.Msg == 0x0021) // WM_MOUSEACTIVATE: dragging must not raise the desktop surface.
            {
                m.Result = (IntPtr)3; // MA_NOACTIVATE, deliver the mouse event.
                return;
            }
            if (m.Msg == Native.WM_NCHITTEST)
            {
                int packed = m.LParam.ToInt32();
                Point p = new Point(unchecked((short)(packed & 0xffff)), unchecked((short)((packed >> 16) & 0xffff)));
                p.Offset(-Bounds.Left, -Bounds.Top);
                bool hit = false;
                foreach (GroupLayout group in Settings.Groups)
                {
                    _renderingGroup = group;
                    if (!group.Collapsed)
                        foreach (DesktopItem item in Settings.Items)
                            if (string.Equals(item.GroupId, group.Id, StringComparison.OrdinalIgnoreCase) &&
                                !IsDesktopMetadata(item.Path) && TileBounds(item).Contains(p)) { hit = true; break; }
                    if (!hit && GroupBounds().Contains(p)) hit = true;
                    if (hit) break;
                }
                _renderingGroup = null;
                m.Result = (IntPtr)(hit ? Native.HTCLIENT : Native.HTTRANSPARENT);
                return;
            }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_titleEditor != null) _titleEditor.Finish(false);
                _reorder.Cancel();
                _dragCancelTimer.Stop();
                _dragCancelTimer.Dispose();
                _itemMenu.Dispose();
                _headerMenu.Dispose();
                _tooltip.Dispose();
                _icons.Dispose();
                _labelFont.Dispose();
                _surface.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
