using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using DeskDecor;

internal static class MenuLifetimeRegression
{
    private static ContextMenuStrip _captured;

    public static void CaptureMenu(ContextMenuStrip menu)
    {
        _captured = menu;
        Expect(!menu.Visible && !menu.IsHandleCreated, "The regression must not show a context menu");
    }

    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private static object Invoke(object target, Type owner, string name, params object[] arguments)
    {
        try { return owner.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }

    private static ContextMenuStrip Open(ListView list)
    {
        Rectangle row = list.Items[0].Bounds;
        Expect(row.Width > 0 && row.Height > 0, "The hidden ListView must have actual row bounds");
        _captured = null;
        Invoke(list, typeof(ListView), "OnMouseUp", new MouseEventArgs(MouseButtons.Right, 1,
            row.Left + 10, row.Top + row.Height / 2, 0));
        Expect(_captured != null && !_captured.IsDisposed, "The settings handler must create a usable menu");
        return _captured;
    }

    [STAThread]
    public static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try
        {
            var settings = new AppSettings { DesktopInitialized = true };
            settings.Group.Title = "Source";
            settings.Groups.Add(settings.Group);
            var target = new GroupLayout { Title = "Target" };
            settings.Groups.Add(target);
            settings.ActiveGroupId = settings.Group.Id;
            var first = new DesktopItem(@"C:\synthetic\first.txt", 0, 0) { GroupId = settings.Group.Id };
            var second = new DesktopItem(@"C:\synthetic\second.txt", 0, 0) { GroupId = settings.Group.Id };
            settings.Items.Add(first);
            settings.Items.Add(second);
            ContextMenuStrip finalMenu;
            using (var form = new SettingsForm(settings))
            {
                var list = (ListView)typeof(SettingsForm).GetField("_list",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                IntPtr hiddenHandle = list.Handle;
                Expect(hiddenHandle != IntPtr.Zero && !form.Visible && !list.Visible,
                    "The test must keep all controls hidden");
                ContextMenuStrip previous = Open(list);
                Invoke(previous, typeof(ToolStripDropDown), "OnClosed",
                    new ToolStripDropDownClosedEventArgs(ToolStripDropDownCloseReason.ItemClicked));
                Expect(!previous.IsDisposed, "Closing a menu must not dispose it inside the click message");
                ToolStripMenuItem move = null;
                foreach (ToolStripItem item in previous.Items)
                    if (item.Text == "移动到分组") move = (ToolStripMenuItem)item;
                Expect(move != null && move.DropDownItems.Count == 1, "The real destination submenu must exist");
                move.DropDownItems[0].PerformClick();
                Expect(first.GroupId == target.Id && second.GroupId == settings.Group.Id && list.Items.Count == 1,
                    "A command after dismissal must move only the selected entry and refresh the source list");
                finalMenu = Open(list);
                Expect(previous.IsDisposed, "Opening the next menu must dispose the previous menu");
                Invoke(finalMenu, typeof(ToolStripDropDown), "OnClosed",
                    new ToolStripDropDownClosedEventArgs(ToolStripDropDownCloseReason.Keyboard));
                Expect(!finalMenu.IsDisposed && !form.Visible, "Dismissal must leave the owned menu alive until safe cleanup");
            }
            Expect(finalMenu.IsDisposed, "Disposing the settings form must dispose its final menu");
            Console.WriteLine("PASS: menu dismissal retains commands, next menu replaces/disposes the previous one, and form disposal releases the final menu");
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error.Message);
            Environment.ExitCode = 1;
        }
    }
}
