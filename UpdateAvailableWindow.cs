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
        var notes = new RichTextBox {
            Text = ConciseNotes(release.Notes), Multiline = true, ReadOnly = true, DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical, WordWrap = true, Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None, BackColor = DarkTheme.Input, ForeColor = DarkTheme.Text,
            Margin = new Padding(0, 0, 0, 12)
        };
        notes.SelectAll(); notes.SelectionIndent = 12; notes.SelectionRightIndent = 12;
        int position = 0;
        foreach (string line in notes.Lines) {
            notes.Select(position, line.Length);
            if (line.StartsWith("Version ")) { notes.SelectionFont = AppFonts.Semibold(11); notes.SelectionColor = DarkTheme.Accent; }
            else if (line.StartsWith("•")) { notes.Select(position, 1); notes.SelectionColor = DarkTheme.Accent; }
            position += line.Length + 1;
        }
        notes.Select(0, 0);
        layout.Controls.Add(new StyledScrollHost(notes){Dock=DockStyle.Fill,Margin=new Padding(0,0,0,12)}, 0, 1);
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
        // WinForms selects the first text field on dialog activation. Start on
        // the non-destructive action while keeping notes selectable for copying.
        Shown += (_, _) => { later.Focus(); notes.Select(0, 0); notes.ScrollToCaret(); };
    }

    internal static string ConciseNotes(string source)
    {
        var lines = source.Replace("\r", "").Split('\n');
        var result = new List<string>();
        foreach (string raw in lines) {
            string line = raw.Trim();
            if (line.StartsWith("Version ") || line == UiText.T("Some release notes could not be loaded. The list below may be incomplete.")) {
                result.Add(line); continue;
            }
            if (!(line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("• "))) continue;
            line = line[2..].Replace("**", "").Replace("`", "");
            line = System.Text.RegularExpressions.Regex.Split(line, @"(?<=[.!?])\s+", System.Text.RegularExpressions.RegexOptions.None)[0];
            result.Add("•  " + line);
        }
        return result.Count > 0 ? string.Join(Environment.NewLine + Environment.NewLine, result)
            : DisplayNotes(source);
    }

    internal static string DisplayNotes(string notes) => string.IsNullOrWhiteSpace(notes)
        ? UiText.T("No release notes were provided.")
        : notes.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
}
