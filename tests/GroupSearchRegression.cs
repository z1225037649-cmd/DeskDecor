using System;
using System.Reflection;
using System.Windows.Forms;
using DeskDecor;

internal static class GroupSearchRegression
{
    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static T Field<T>(SettingsForm form, string name) where T : class
    {
        return (T)typeof(SettingsForm).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
    }

    private static int IndexOfGroup(ComboBox groups, string id)
    {
        for (int i = 0; i < groups.Items.Count; i++)
        {
            string candidate = (string)groups.Items[i].GetType()
                .GetField("Id", BindingFlags.Instance | BindingFlags.Public).GetValue(groups.Items[i]);
            if (string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    [STAThread]
    public static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try
        {
            var settings = new AppSettings { DesktopInitialized = true };
            GroupLayout source = settings.Group;
            source.Title = "Source Alpha";
            var target = new GroupLayout { Title = "Target Beta", X = 420, Y = 40, Width = 320, Height = 260 };
            settings.Groups.Add(source);
            settings.Groups.Add(target);
            settings.ActiveGroupId = source.Id;

            var aliased = new DesktopItem(@"C:\qa-source\reports\annual.xlsx", 0, 0)
                { Label = "Quarterly Budget Alias", GroupId = source.Id };
            var plain = new DesktopItem(@"C:\qa-source\notes\source-notes.txt", 0, 0)
                { GroupId = source.Id };
            var targetAliased = new DesktopItem(@"C:\qa-target\reports\budget.txt", 0, 0)
                { Label = "Target Budget", GroupId = target.Id };
            var targetPlain = new DesktopItem(@"C:\qa-target\archive\old-plan.txt", 0, 0)
                { GroupId = target.Id };
            settings.Items.Add(aliased);
            settings.Items.Add(plain);
            settings.Items.Add(targetAliased);
            settings.Items.Add(targetPlain);

            using (var overlay = new OverlayForm(settings))
            using (var form = new SettingsForm(settings))
            {
                TextBox search = Field<TextBox>(form, "_search");
                ListView list = Field<ListView>(form, "_list");
                ComboBox groups = Field<ComboBox>(form, "_groups");
                int formGroupChanges = 0;
                int overlayGroupChanges = 0;
                int dataChanges = 0;

                form.SettingsChanged += delegate { dataChanges++; };
                form.GroupChanged += delegate
                {
                    formGroupChanges++;
                    overlay.SetActiveGroup(settings.ActiveGroupId);
                };
                overlay.ActiveGroupChanged += delegate
                {
                    overlayGroupChanges++;
                    // This is the production ActiveGroupChanged refresh path in AppContext.
                    form.RefreshItems();
                    form.RefreshGroups();
                };

                Expect(groups.Items.Count == 2 && groups.GetItemText(groups.Items[0]) == "Source Alpha" &&
                    groups.GetItemText(groups.Items[1]) == "Target Beta",
                    "The selector must display every group title in settings order");
                Expect(form.ActiveGroupId == source.Id && list.Items.Count == 2,
                    "The initial selector and list must reflect only the active source group");

                search.Text = "  qUaRtErLy BuDgEt aLiAs  ";
                Expect(list.Items.Count == 1 && ReferenceEquals(list.Items[0].Tag, aliased),
                    "Search must trim whitespace, ignore case, and match the displayed custom alias");
                search.Text = "SOURCE-NOTES.TXT";
                Expect(list.Items.Count == 1 && ReferenceEquals(list.Items[0].Tag, plain),
                    "Search must match an unlabeled entry by its displayed file name");
                search.Text = "QA-SOURCE";
                Expect(list.Items.Count == 2,
                    "Search must match the full path, ignore case, and exclude entries from other groups");
                search.Text = "  \t ";
                Expect(list.Items.Count == 2,
                    "A whitespace-only query must behave like an empty query");
                search.Text = "no-such-entry";
                Expect(list.Items.Count == 0,
                    "A query without matches must leave the current group's list empty");

                // Keep the active filter while selecting another group. The form updates shared
                // settings first; the overlay then raises its production change event once.
                search.Text = "budget";
                groups.SelectedIndex = IndexOfGroup(groups, target.Id);
                Expect(settings.ActiveGroupId == target.Id && form.ActiveGroupId == target.Id,
                    "Changing the selector must synchronize the shared active group id");
                Expect(formGroupChanges == 1 && overlayGroupChanges == 1,
                    "One selector change must notify the overlay once despite the shared id changing first");
                Expect(list.Items.Count == 1 && ReferenceEquals(list.Items[0].Tag, targetAliased),
                    "Switching groups must retain the search query and filter the destination group's list");

                target.Title = "Target Renamed";
                form.RefreshItems();
                Expect(groups.GetItemText(groups.SelectedItem) == "Target Renamed" &&
                    form.ActiveGroupId == target.Id && list.Items.Count == 1,
                    "Refreshing items and groups must update the title while preserving the current selection and filter");
                Expect(formGroupChanges == 1 && overlayGroupChanges == 1,
                    "Refreshing the selector must not emit a recursive group-change notification");

                // A group change originating at the overlay must synchronize the settings form too.
                overlay.SetActiveGroup(source.Id);
                Expect(settings.ActiveGroupId == source.Id && form.ActiveGroupId == source.Id &&
                    groups.SelectedIndex == IndexOfGroup(groups, source.Id),
                    "An overlay-originated group change must synchronize the form selector and shared settings");
                Expect(overlayGroupChanges == 2 && formGroupChanges == 1 && list.Items.Count == 1 &&
                    ReferenceEquals(list.Items[0].Tag, aliased),
                    "The overlay change must refresh the list for the newly active group without a reverse event");

                search.Text = string.Empty;
                Expect(list.Items.Count == 2,
                    "Clearing search must restore all entries in the synchronized active group");
                form.RefreshItems();
                form.RefreshGroups();
                overlay.SetActiveGroup(source.Id);
                Expect(formGroupChanges == 1 && overlayGroupChanges == 2 &&
                    settings.ActiveGroupId == source.Id && list.Items.Count == 2,
                    "Repeated refreshes and selecting the already active group must be quiet");

                Expect(dataChanges == 0 && settings.Items.Count == 4 &&
                    aliased.Label == "Quarterly Budget Alias" && plain.GroupId == source.Id,
                    "Searching and changing groups must not edit entries or raise entry-change events");
                Expect(!form.Visible && !form.IsHandleCreated && !overlay.Visible && !overlay.IsHandleCreated,
                    "The real controls must remain hidden and must not create desktop window handles");
            }

            Console.WriteLine("PASS: search filters current-group labels, names and paths; empty, whitespace and unmatched queries behave correctly");
            Console.WriteLine("PASS: form and overlay group selection synchronize through the production event flow; RefreshGroups does not recurse");
            Console.WriteLine("PASS: regression used hidden real forms without visible windows or data edits");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            Environment.ExitCode = 1;
        }
    }
}
