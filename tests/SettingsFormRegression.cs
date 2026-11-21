using System;
using System.Reflection;
using System.Windows.Forms;
using DeskDecor;

internal static class SettingsFormRegression
{
    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private static T Field<T>(SettingsForm form, string name) where T : class
    {
        return (T)typeof(SettingsForm).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
    }

    [STAThread] public static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try
        {
            var settings = new AppSettings { DesktopInitialized = true };
            settings.Group.Title = "Source";
            settings.Groups.Add(settings.Group);
            var target = new GroupLayout { Title = "Target", X = 500, Y = 40, Width = 320, Height = 300 };
            settings.Groups.Add(target);
            settings.ActiveGroupId = settings.Group.Id;
            var sourceItem = new DesktopItem(@"C:\qa-source\report.txt", 0, 0)
                { Label = "Quarterly Budget", GroupId = settings.Group.Id };
            var secondItem = new DesktopItem(@"C:\qa-source\notes.txt", 0, 0)
                { GroupId = settings.Group.Id };
            var targetItem = new DesktopItem(@"C:\qa-target\report.txt", 0, 0)
                { Label = "Target Budget", GroupId = target.Id };
            settings.Items.Add(sourceItem);
            settings.Items.Add(secondItem);
            settings.Items.Add(targetItem);

            using (var overlay = new OverlayForm(settings))
            using (var form = new SettingsForm(settings))
            {
                var search = Field<TextBox>(form, "_search");
                var list = Field<ListView>(form, "_list");
                var groups = Field<ComboBox>(form, "_groups");
                int groupNotifications = 0, overlayNotifications = 0, dataNotifications = 0;
                form.SettingsChanged += delegate { dataNotifications++; };
                form.GroupChanged += delegate
                {
                    groupNotifications++;
                    overlay.SetActiveGroup(settings.ActiveGroupId);
                };
                overlay.ActiveGroupChanged += delegate
                {
                    overlayNotifications++;
                    // The application refreshes the selector in this callback.
                    form.RefreshItems();
                };

                Expect(list.Items.Count == 2, "Initial list must contain only the active group's entries");
                search.Text = "  BUDGET  ";
                Expect(list.Items.Count == 1 && ReferenceEquals(list.Items[0].Tag, sourceItem),
                    "Search must trim whitespace, ignore case and match custom labels without leaking another group");
                search.Text = "QA-SOURCE";
                Expect(list.Items.Count == 2, "Search must also match full paths ignoring case");
                search.Text = "missing";
                Expect(list.Items.Count == 0, "A query without matches must produce an empty list");
                search.Text = "budget";

                groups.SelectedIndex = 1;
                Expect(settings.ActiveGroupId == target.Id && form.ActiveGroupId == target.Id,
                    "Changing the selector must update the active group");
                Expect(groupNotifications == 1 && overlayNotifications == 1,
                    "The overlay must notify exactly once even when the settings form has already updated the shared group id");
                Expect(list.Items.Count == 1 && ReferenceEquals(list.Items[0].Tag, targetItem),
                    "Switching groups must retain the query and display only matching destination entries");
                form.RefreshItems();
                form.RefreshGroups();
                overlay.SetActiveGroup(target.Id);
                overlay.SetActiveGroup("missing-group");
                Expect(groupNotifications == 1 && overlayNotifications == 1 && settings.ActiveGroupId == target.Id,
                    "Refresh, repeated selection and invalid ids must not trigger another group change");

                search.Text = string.Empty;
                groups.SelectedIndex = 0;
                Expect(list.Items.Count == 2 && groupNotifications == 2 && overlayNotifications == 2,
                    "Clearing search and switching back must restore all source entries with one notification");
                Expect(dataNotifications == 0 && settings.Items.Count == 3 && sourceItem.Label == "Quarterly Budget",
                    "Filtering and group selection must not mutate entry data or emit an entry edit");
                Expect(!form.Visible && !form.IsHandleCreated && !overlay.Visible && !overlay.IsHandleCreated,
                    "Control regressions must not show or attach desktop windows");
            }
            Console.WriteLine("PASS: settings search isolates groups and matches labels and paths without editing entries");
            Console.WriteLine("PASS: settings group selection notifies the overlay once and refresh does not recurse (no visible test windows)");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            Environment.ExitCode = 1;
        }
    }
}
