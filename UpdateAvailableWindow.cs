namespace SimpleAimOverlay;

internal sealed class UpdateAvailableWindow : StyledDialog
{
    public UpdateAvailableWindow(AppUpdater.Release release)
    {
        Text = "Update available";
        ClientSize = new Size(620, 510);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.Controls.Add(new Label { Text = UiText.T("What's new") + " · " + release.Version.ToString(3), Dock = DockStyle.Fill, AutoSize = false }, 0, 0);
        // Release content is plain text, never executable HTML or navigable remote content.
        layout.Controls.Add(new TextBox {
            Text = DisplayNotes(release.Notes), Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, WordWrap = true, Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None, BackColor = DarkTheme.Input, ForeColor = DarkTheme.Text,
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 1);
        layout.Controls.Add(new Label { Text = "You will confirm installation after the download. Your settings will be kept.", Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) }, 0, 2);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var later = ActionButton("Not now", DialogResult.Cancel);
        var download = ActionButton("Download update", DialogResult.OK);
        later.Dock = download.Dock = DockStyle.Fill; later.Margin = download.Margin = Padding.Empty;
        actions.Controls.Add(later, 0, 0); actions.Controls.Add(download, 2, 0);
        layout.Controls.Add(actions, 0, 3); Controls.Add(layout);
        CancelButton = later;
    }

    internal static string DisplayNotes(string notes) => string.IsNullOrWhiteSpace(notes)
        ? UiText.T("No release notes were provided.")
        : notes.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
}
