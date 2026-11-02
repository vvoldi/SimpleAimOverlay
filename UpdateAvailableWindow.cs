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
            else if (line == FeaturesHeading || line == FixesHeading) { notes.SelectionFont = AppFonts.Semibold(10); notes.SelectionColor = DarkTheme.Text; }
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

    internal static string FeaturesHeading => UiText.Language == "uk" ? "Нові можливості" : "Features";
    internal static string FixesHeading => UiText.Language == "uk" ? "Виправлення" : "Fixes";

    internal static string ConciseNotes(string source)
    {
        var result = new List<string>();
        var features = new List<string>();
        var fixes = new List<string>();
        bool? fixSection = null;
        bool skipSection = false;
        void Flush() {
            if(features.Count>0){result.Add(FeaturesHeading);result.AddRange(features);features.Clear();}
            if(fixes.Count>0){result.Add(FixesHeading);result.AddRange(fixes);fixes.Clear();}
        }
        foreach(string raw in source.Replace("\r", "").Split('\n')) {
            string line=raw.Trim();
            if(line.StartsWith("Version ") || line==UiText.T("Some release notes could not be loaded. The list below may be incomplete.")) {
                Flush();result.Add(line);fixSection=null;skipSection=false;continue;
            }
            string heading=line.TrimStart('#').Trim().Trim('*').TrimEnd(':').Trim().ToLowerInvariant();
            if(heading is "features" or "feature" or "new features" or "improvements" or "нові можливості") {fixSection=false;skipSection=false;continue;}
            if(heading is "fixes" or "fix" or "bug fixes" or "bugfixes" or "виправлення") {fixSection=true;skipSection=false;continue;}
            if(heading is "validation" or "testing" or "installation" or "checksums") {skipSection=true;continue;}
            if(skipSection || !(line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("• ")))continue;
            line=line[2..].Replace("**", "").Replace("`", "");
            // Older releases have no sections; keep their items and classify explicit fix wording.
            bool isFix=fixSection ?? System.Text.RegularExpressions.Regex.IsMatch(line,@"^(fix(?:ed|es)?|resolved?|correct(?:ed|s)?|repair(?:ed|s)?|prevent(?:ed|s)?)\b|\b(bug|flicker(?:ing)?|crash(?:es|ing)?|regression)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            line=System.Text.RegularExpressions.Regex.Split(line,@"(?<=[.!?])\s+")[0];
            (isFix?fixes:features).Add("•  "+line);
        }
        Flush();
        return result.Count>0 ? string.Join(Environment.NewLine+Environment.NewLine,result) : DisplayNotes(source);
    }
    internal static string DisplayNotes(string notes) => string.IsNullOrWhiteSpace(notes)
        ? UiText.T("No release notes were provided.")
        : notes.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
}
