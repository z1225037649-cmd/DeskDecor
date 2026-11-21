using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;
using DeskDecor;

// Only menu.Show and rename-dialog.ShowDialog are substituted in a temporary
// SettingsForm source copy. All controls, callbacks and mutation branches are real.
internal static class EntryGroupRegression
{
    private static ContextMenuStrip _menu;
    private static DialogResult _answer;
    private static string _input, _expectedInput;
    private static int _dialogs;
    private static int _failed;

    public static void CaptureMenu(ContextMenuStrip menu)
    {
        Expect(_menu == null, "Previous context menu was not disposed");
        Expect(!menu.Visible && !menu.IsHandleCreated, "Context menu must never be displayed");
        _menu = menu;
    }

    public static DialogResult AnswerRename(Form dialog)
    {
        Expect(!dialog.Visible && !dialog.IsHandleCreated, "Rename dialog must never be displayed");
        TextBox input = null;
        foreach (Control child in dialog.Controls) if (child is TextBox) input = (TextBox)child;
        Expect(input != null && input.Text == _expectedInput, "Rename dialog must initialize from the displayed name");
        Expect(((Button)dialog.AcceptButton).DialogResult == DialogResult.OK &&
            ((Button)dialog.CancelButton).DialogResult == DialogResult.Cancel,
            "Real save/cancel buttons must expose the expected dialog results");
        input.Text = _input;
        _dialogs++;
        return _answer;
    }

    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private static object Invoke(object target, string method, params object[] args)
    {
        try { return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }

    private static T Field<T>(object target, string name)
    { return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target); }

    private sealed class Fixture : IDisposable
    {
        public readonly string Root;
        public readonly AppSettings Settings;
        public readonly GroupLayout Source, Target, Third;
        public readonly SettingsForm Form;
        public readonly OverlayForm Overlay;
        public readonly ListView List;
        public readonly ComboBox Groups;
        public readonly DesktopItem A, B, C, Existing;
        public int Changed, Moved, ItemsChanged, ActiveChanged, GroupChanged, RemoveRequested;
        public readonly List<string> Events = new List<string>();
        private readonly Dictionary<string, string> _originals = new Dictionary<string, string>();
        private readonly Dictionary<string, long> _timestamps = new Dictionary<string, long>();
        private readonly List<DesktopItem> _identities;
        private readonly Dictionary<DesktopItem, string> _paths = new Dictionary<DesktopItem, string>();
        private readonly Dictionary<DesktopItem, string> _names = new Dictionary<DesktopItem, string>();

        public Fixture()
        {
            Root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Path.Combine(Root, "folder"));
            Directory.CreateDirectory(Path.Combine(Root, "empty-folder"));
            foreach (string name in new[] { "alpha.txt", "beta.txt", "folder\\nested.txt", "existing.txt" })
            {
                string path = Path.Combine(Root, name);
                File.WriteAllText(path, "Original bytes: " + name + "\r\n保留原文件");
                _originals[path] = Hash(path);
                _timestamps[path] = File.GetLastWriteTimeUtc(path).Ticks;
            }
            Settings = new AppSettings { DesktopInitialized = true };
            Source = Settings.Group;
            Source.Title = "Source"; Source.X = 20; Source.Y = 20; Source.Width = 300; Source.Height = 300;
            Target = new GroupLayout { Title = "Target", X = 420, Y = 20, Width = 300, Height = 300 };
            Third = new GroupLayout { Title = "Third", X = 820, Y = 20, Width = 300, Height = 300 };
            Settings.Groups.AddRange(new[] { Source, Target, Third });
            Settings.ActiveGroupId = Source.Id;
            A = new DesktopItem(Path.Combine(Root, "alpha.txt"), 0, 0) { GroupId = Source.Id };
            B = new DesktopItem(Path.Combine(Root, "beta.txt"), 0, 0) { GroupId = Source.Id, Label = "Custom Beta" };
            C = new DesktopItem(Path.Combine(Root, "folder"), 0, 0) { GroupId = Source.Id };
            Existing = new DesktopItem(Path.Combine(Root, "existing.txt"), 0, 0) { GroupId = Target.Id };
            Settings.Items.AddRange(new[] { A, B, C, Existing });
            _identities = new List<DesktopItem>(Settings.Items);
            foreach (DesktopItem item in Settings.Items) { _paths[item] = item.Path; _names[item] = item.Name; }
            Overlay = new OverlayForm(Settings);
            Form = new SettingsForm(Settings);
            List = Field<ListView>(Form, "_list");
            Groups = Field<ComboBox>(Form, "_groups");
            // Native selection/hit-testing requires a ListView HWND, kept under
            // a never-shown settings form. The desktop overlay has no HWND.
            IntPtr hiddenListHandle = List.Handle;
            Expect(hiddenListHandle != IntPtr.Zero && !Form.Visible && !List.Visible, "ListView must stay hidden");
            Form.SettingsChanged += delegate { Changed++; Events.Add("settings"); Overlay.RefreshItemsLayout(); Save(); };
            Form.GroupChanged += delegate { GroupChanged++; Overlay.SetActiveGroup(Settings.ActiveGroupId); };
            Overlay.ActiveGroupChanged += delegate { ActiveChanged++; Form.RefreshItems(); Form.RefreshGroups(); Save(); };
            Overlay.ItemMoved += delegate { Moved++; Events.Add("moved"); Save(); };
            Overlay.ItemsChanged += delegate { ItemsChanged++; Events.Add("items"); Form.RefreshItems(); };
            Form.RemoveGroupRequested += delegate
            {
                RemoveRequested++;
                Overlay.RemoveActiveGroup();
            };
        }

        private void Save() { SettingsStore.Save(Path.Combine(Root, "settings.xml"), Settings); }

        public void Select(params DesktopItem[] items)
        {
            List.SelectedItems.Clear();
            foreach (ListViewItem row in List.Items)
                row.Selected = Array.IndexOf(items, (DesktopItem)row.Tag) >= 0;
            Expect(List.SelectedItems.Count == items.Length, "Real ListView selection did not match requested entries");
        }

        public void OpenMenu(DesktopItem clicked)
        {
            foreach (ListViewItem row in List.Items)
                if (ReferenceEquals(row.Tag, clicked))
                {
                    Rectangle bounds = row.Bounds;
                    Expect(bounds.Width > 0 && bounds.Height > 0, "Hidden ListView needs actual row bounds");
                    Invoke(List, "OnMouseUp", new MouseEventArgs(MouseButtons.Right, 1, bounds.Left + 10, bounds.Top + bounds.Height / 2, 0));
                    Expect(_menu != null, "Real MouseUp handler did not build a context menu");
                    return;
                }
            throw new Exception("Clicked entry is not in the active filtered list");
        }

        public void MoveFromMenu(string destination)
        {
            try
            {
                ToolStripMenuItem move = null;
                foreach (ToolStripItem entry in _menu.Items) if (entry.Text == "移动到分组") move = (ToolStripMenuItem)entry;
                Expect(move != null && move.DropDownItems.Count == Settings.Groups.Count - 1,
                    "Move menu must exclude exactly the current group");
                ToolStripItem found = null;
                foreach (ToolStripItem entry in move.DropDownItems) if (entry.Text == destination) found = entry;
                Expect(found != null, "Destination missing from move menu");
                found.PerformClick();
            }
            finally { if (_menu != null) { _menu.Dispose(); _menu = null; } }
        }

        public void Rename(string initial, string text, DialogResult answer)
        {
            _expectedInput = initial; _input = text; _answer = answer;
            Invoke(Form, "RenameSelectedItem");
        }

        public void Switch(int index) { Groups.SelectedIndex = index; }

        public void DeleteButton()
        {
            foreach (Control control in Form.Controls)
                if (control is Button && control.Text == "删除分组") { Invoke(control, "OnClick", EventArgs.Empty); return; }
            throw new Exception("Delete group button not found");
        }

        public void ExpectRows(params DesktopItem[] items)
        {
            Expect(List.Items.Count == items.Length, "Wrong number of rows for the active group/query");
            for (int i = 0; i < items.Length; i++) Expect(ReferenceEquals(List.Items[i].Tag, items[i]), "Wrong entry or order in settings rows");
            Expect(Form.ActiveGroupId == Settings.ActiveGroupId &&
                Field<string>(Overlay, "_selectedGroupId") == Settings.ActiveGroupId,
                "Settings selector and overlay selection diverged");
        }

        public void ExpectLayout()
        {
            foreach (GroupLayout group in Settings.Groups)
            {
                var tiles = new List<Rectangle>();
                foreach (DesktopItem item in Settings.Items)
                {
                    if (!string.Equals(item.GroupId, group.Id, StringComparison.OrdinalIgnoreCase)) continue;
                    var tile = new Rectangle(item.X, item.Y, MonitorDpi.Scale(76, group.Dpi), MonitorDpi.Scale(66, group.Dpi));
                    Expect(new Rectangle(group.X, group.Y, group.Width, group.Height).Contains(tile), "Moved entry lies outside its destination group");
                    foreach (Rectangle other in tiles) Expect(!tile.IntersectsWith(other), "Moved entries overlap after layout refresh");
                    tiles.Add(tile);
                }
            }
        }

        public void ExpectPreserved()
        {
            Expect(Settings.Items.Count == _identities.Count, "Entry count changed");
            for (int i = 0; i < _identities.Count; i++)
            {
                DesktopItem item = _identities[i];
                Expect(ReferenceEquals(Settings.Items[i], item) && item.Path == _paths[item] && item.Name == _names[item],
                    "Entry identity, global order, original path or original name changed");
                Expect(Settings.Groups.Exists(g => string.Equals(g.Id, item.GroupId, StringComparison.OrdinalIgnoreCase)), "Entry has an orphaned group id");
            }
            foreach (var pair in _originals)
                Expect(File.Exists(pair.Key) && Hash(pair.Key) == pair.Value && File.GetLastWriteTimeUtc(pair.Key).Ticks == _timestamps[pair.Key],
                    "Original file was lost, renamed, rewritten or touched: " + pair.Key);
            Expect(Directory.Exists(C.Path) && Directory.Exists(Path.Combine(Root, "empty-folder")) &&
                Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Length == _originals.Count +
                    (File.Exists(Path.Combine(Root, "settings.xml")) ? 1 : 0) + (File.Exists(Path.Combine(Root, "settings.xml.bak")) ? 1 : 0),
                "Unexpected renamed copies, missing folders or extra files");
            Expect(!Form.Visible && !Overlay.Visible && !Overlay.IsHandleCreated, "A regression created a visible or desktop-attached window");
        }

        public void ExpectRoundTrip()
        {
            Save();
            AppSettings restored = SettingsStore.Load(Path.Combine(Root, "settings.xml"));
            Expect(restored.Groups.Count == Settings.Groups.Count && restored.Items.Count == Settings.Items.Count &&
                restored.ActiveGroupId == Settings.ActiveGroupId, "Group membership/selection count did not persist");
            for (int i = 0; i < Settings.Items.Count; i++)
                Expect(restored.Items[i].GroupId == Settings.Items[i].GroupId && restored.Items[i].Label == Settings.Items[i].Label &&
                    restored.Items[i].Path == Settings.Items[i].Path, "Membership, display label or original path changed across save/load");
        }

        public void Dispose()
        {
            if (_menu != null) { _menu.Dispose(); _menu = null; }
            Form.Dispose(); Overlay.Dispose();
            Directory.Delete(Root, true);
        }
    }

    private static string Hash(string path)
    {
        using (var sha = SHA256.Create()) using (var file = File.OpenRead(path)) return Convert.ToBase64String(sha.ComputeHash(file));
    }

    private static void Run(string title, Action test)
    {
        try { test(); Console.WriteLine("PASS: " + title); }
        catch (Exception ex) { _failed++; Console.WriteLine("FAIL: " + title + " :: " + ex.Message); }
    }

    [STAThread] public static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Run("overlay single-entry move, exact event order, inactive destination layout and no-op guards", delegate
        {
            using (var f = new Fixture())
            {
                f.Overlay.MoveItemToGroup(f.A, f.Target.Id.ToUpperInvariant());
                Expect(string.Equals(f.A.GroupId, f.Target.Id, StringComparison.OrdinalIgnoreCase), "Overlay move did not assign destination");
                Expect(f.Moved == 1 && f.ItemsChanged == 1 && f.Changed == 0 && f.ActiveChanged == 0 &&
                    string.Join(",", f.Events.ToArray()) == "moved,items", "Unexpected overlay move notification sequence");
                f.ExpectRows(f.B, f.C); f.ExpectLayout();
                f.Overlay.MoveItemToGroup(f.A, f.Target.Id);
                f.Overlay.MoveItemToGroup(f.B, "missing-group");
                f.Overlay.MoveItemToGroup(null, f.Target.Id);
                Expect(f.Moved == 1 && f.ItemsChanged == 1 && f.B.GroupId == f.Source.Id, "Invalid/same destination must be a no-op");
                f.Switch(1); f.ExpectRows(f.A, f.Existing); f.ExpectPreserved(); f.ExpectRoundTrip();
            }
        });
        Run("settings context move replaces selection on an unselected row and refreshes both groups", delegate
        {
            using (var f = new Fixture())
            {
                f.Select(f.B); f.OpenMenu(f.A);
                Expect(f.List.SelectedItems.Count == 1 && ReferenceEquals(f.List.SelectedItems[0].Tag, f.A), "Right-click on an unselected row must select only that row");
                f.MoveFromMenu("Target");
                Expect(f.A.GroupId == f.Target.Id && f.B.GroupId == f.Source.Id && f.Changed == 1 && f.Moved == 0, "Single settings move changed the wrong entries or events");
                f.ExpectRows(f.B, f.C); f.ExpectLayout(); f.Switch(1); f.ExpectRows(f.A, f.Existing);
                f.ExpectRoundTrip(); f.ExpectPreserved();
            }
        });
        Run("settings batch selection moves exactly selected entries once and retains order/labels", delegate
        {
            using (var f = new Fixture())
            {
                f.Select(f.A, f.C); f.OpenMenu(f.A);
                Expect(f.List.SelectedItems.Count == 2, "Right-click on a selected row must preserve batch selection");
                f.MoveFromMenu("Target");
                Expect(f.A.GroupId == f.Target.Id && f.C.GroupId == f.Target.Id && f.B.GroupId == f.Source.Id &&
                    f.Changed == 1 && f.ItemsChanged == 0 && f.Moved == 0, "Batch move must emit one settings change and move exactly the selection");
                f.ExpectRows(f.B); f.Switch(1); f.ExpectRows(f.A, f.C, f.Existing); f.ExpectLayout();
                f.ExpectPreserved(); f.ExpectRoundTrip();
            }
        });
        Run("filtered settings batch move preserves query and excludes hidden source entries", delegate
        {
            using (var f = new Fixture())
            {
                Field<TextBox>(f.Form, "_search").Text = ".txt";
                f.ExpectRows(f.A, f.B); f.Select(f.A, f.B); f.OpenMenu(f.B); f.MoveFromMenu("Target");
                f.ExpectRows(); f.Switch(1); f.ExpectRows(f.A, f.B, f.Existing);
                Expect(f.C.GroupId == f.Source.Id && Field<TextBox>(f.Form, "_search").Text == ".txt" && f.Changed == 1,
                    "Filtered batch must retain query and leave hidden entries in their original group");
                f.ExpectLayout(); f.ExpectRoundTrip(); f.ExpectPreserved();
            }
        });
        Run("display-name save trims text, updates search and persists without renaming originals", delegate
        {
            using (var f = new Fixture())
            {
                f.Select(f.A); f.Rename("alpha.txt", "  Display Report / 显示名称  ", DialogResult.OK);
                Expect(f.A.Label == "Display Report / 显示名称" && f.List.Items[0].Text == f.A.Label && f.Changed == 1,
                    "Display-name save did not trim, update list or notify exactly once");
                Field<TextBox>(f.Form, "_search").Text = "显示名称"; f.ExpectRows(f.A);
                f.Select(f.A); f.Rename(f.A.Label, "Second Label", DialogResult.OK); f.ExpectRows();
                Expect(f.A.Label == "Second Label" && f.Changed == 2, "Renaming must reevaluate current search");
                f.ExpectRoundTrip(); f.ExpectPreserved();
            }
        });
        Run("display-name cancel/blank and zero/multiple selection never mutate or notify", delegate
        {
            using (var f = new Fixture())
            {
                f.Select(f.B); f.Rename("Custom Beta", "Discard me", DialogResult.Cancel);
                f.Select(f.B); f.Rename("Custom Beta", "   ", DialogResult.OK);
                f.Select(f.B); f.Rename("Custom Beta", "", DialogResult.OK);
                int before = _dialogs;
                f.Select(); f.Rename("ignored", "ignored", DialogResult.OK);
                f.Select(f.A, f.B); f.Rename("ignored", "ignored", DialogResult.OK);
                Expect(_dialogs == before && f.Changed == 0 && f.B.Label == "Custom Beta" && f.A.Label == "",
                    "Cancel, blank or ambiguous selection must not change labels or emit a save");
                f.ExpectPreserved();
            }
        });
        Run("delete active middle group migrates entries to first remaining group and syncs controls", delegate
        {
            using (var f = new Fixture())
            {
                f.Overlay.MoveItemToGroup(f.A, f.Target.Id); f.Switch(1);
                int moved = f.Moved, items = f.ItemsChanged, active = f.ActiveChanged;
                f.DeleteButton();
                Expect(f.RemoveRequested == 1 && f.Settings.Groups.Count == 2 && f.A.GroupId == f.Source.Id &&
                    f.Existing.GroupId == f.Source.Id && f.Moved == moved + 1 && f.ItemsChanged == items + 1 && f.ActiveChanged == active,
                    "Delete must migrate all removed-group entries and issue its actual moved/items notifications");
                f.ExpectRows(f.A, f.B, f.C, f.Existing);
                Expect(f.Groups.Items.Count == 2 && f.Groups.SelectedIndex == 0, "Deleted group remains in selector");
                f.ExpectLayout(); f.ExpectRoundTrip(); f.ExpectPreserved();
            }
        });
        Run("delete first/empty groups retains every entry; last-group deletion is blocked", delegate
        {
            using (var f = new Fixture())
            {
                f.DeleteButton();
                Expect(f.Settings.Groups.Count == 2 && f.Settings.ActiveGroupId == f.Target.Id,
                    "Deleting first group must advance active selection");
                f.ExpectRows(f.A, f.B, f.C, f.Existing); f.ExpectLayout(); f.ExpectRoundTrip();
                f.Switch(1); f.ExpectRows(); f.DeleteButton();
                f.ExpectRows(f.A, f.B, f.C, f.Existing);
                int moved = f.Moved, items = f.ItemsChanged;
                f.DeleteButton();
                Expect(f.Settings.Groups.Count == 1 && f.Moved == moved && f.ItemsChanged == items && !f.Overlay.RemoveActiveGroup(),
                    "Last group deletion must be a no-op without item notifications");
                f.ExpectRoundTrip(); f.ExpectPreserved();
            }
        });
        Run("deleted first group must not remain in the legacy Group field or saved XML", delegate
        {
            using (var f = new Fixture())
            {
                f.DeleteButton();
                f.ExpectRows(f.A, f.B, f.C, f.Existing); f.ExpectLayout(); f.ExpectRoundTrip(); f.ExpectPreserved();
                // XmlSerializer exposes the unnormalized serialized state. Load()
                // repairs the alias, so a normal round trip alone hides this defect.
                AppSettings raw;
                using (var stream = File.OpenRead(Path.Combine(f.Root, "settings.xml")))
                    raw = (AppSettings)new System.Xml.Serialization.XmlSerializer(typeof(AppSettings)).Deserialize(stream);
                Expect(f.Settings.Groups.Contains(f.Settings.Group) && raw.Groups.Exists(g => g.Id == raw.Group.Id),
                    "RemoveActiveGroup removed Source from Groups but retained Source in Settings.Group and <Group>; " +
                    "entry membership, active selection and normalized reload still pass");
            }
        });
        Console.WriteLine("RESULT: " + (9 - _failed) + "/9 checks passed; presentation suppressed; overlay HWND never created");
        Environment.ExitCode = _failed == 0 ? 0 : 1;
    }
}
