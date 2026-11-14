using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using DeskDecor;

internal static class InlineTitleRegression
{
    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    private static void Key(TextBox input, Keys key)
    {
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(input, new object[] { new KeyEventArgs(key) });
    }
    private static void Ime(TextBox input, int message)
    {
        object[] args = { Message.Create(input.Handle, message, IntPtr.Zero, IntPtr.Zero) };
        input.GetType().GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, args);
    }
    [STAThread] public static void Main()
    {
        // Do not show native test windows or let a failed assertion open a WER
        // dialog on the user's interactive desktop.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try { Run(); }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.GetType().Name + ": " + ex.Message);
            if (ex.InnerException != null) Console.Error.WriteLine(ex.InnerException.GetType().Name + ": " + ex.InnerException.Message);
            Environment.ExitCode = 1;
        }
    }
    private static void Run()
    {
        Application.EnableVisualStyles();
        Rectangle title = new Rectangle(20, 20, 180, 26);
        using (var editor = new InlineTitleEditor("快捷入口", title))
        {
            Expect(editor.FormBorderStyle == FormBorderStyle.None && editor.Bounds == title && !editor.ShowInTaskbar && !editor.TopMost,
                "Inline input must occupy the title bounds without dialog chrome or TopMost");
            TextBox input = (TextBox)editor.Controls[0];
            Expect(!editor.Visible, "Regression checks must never show a test window");
            input.Text = "  常用工具  ";
            Key(input, Keys.Enter);
            Expect(editor.SaveChanges && editor.Value == "常用工具", "Enter must save trimmed text once");
            editor.Finish(false);
            Expect(editor.SaveChanges && editor.Value == "常用工具", "Finishing twice must not change the committed result");
        }
        using (var editor = new InlineTitleEditor("原名称", title))
        {
            TextBox input = (TextBox)editor.Controls[0];
            input.Text = "不保存";
            Key(input, Keys.Escape);
            Expect(!editor.SaveChanges && !editor.Visible, "Escape must cancel instead of saving");
        }
        using (var editor = new InlineTitleEditor("快捷入口", title))
        {
            TextBox input = (TextBox)editor.Controls[0];
            Ime(input, 0x010D);
            Key(input, Keys.Enter);
            Expect(!editor.IsDisposed && !editor.SaveChanges, "Enter must not close the editor while accepting an IME candidate");
            Ime(input, 0x010E);
            Key(input, Keys.Enter);
            Expect(editor.SaveChanges && !editor.Visible, "Enter after IME completion must save");
        }
        using (var editor = new InlineTitleEditor("快捷入口", title))
        {
            typeof(Form).GetMethod("OnDeactivate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(editor, new object[] { EventArgs.Empty });
            Expect(editor.SaveChanges && !editor.Visible, "Clicking elsewhere must finish with save");
        }
        Console.WriteLine("PASS: inline bounds, Enter, Escape, IME composition and focus-loss save (no visible test windows)");
    }
}
