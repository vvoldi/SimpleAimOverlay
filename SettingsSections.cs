namespace SimpleAimOverlay;

internal sealed class SettingsSections : UserControl
{
    private readonly List<(Button Button, Panel Page)> sections = [];
    internal int SelectedIndex { get; private set; } = -1;
    internal SettingsSections(TableLayoutPanel source)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 225)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var sidebar = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8,12,8,12), BackColor = DarkTheme.Window };
        var nav = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 0 };
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); sidebar.Controls.Add(nav);
        var content = new Panel { Dock = DockStyle.Fill, BackColor = DarkTheme.Surface };
        layout.Controls.Add(sidebar,0,0); layout.Controls.Add(content,1,0); Controls.Add(layout);
        TableLayoutPanel? target = null;
        for (int row = 0; row < source.RowCount; row++) {
            var cells = source.Controls.Cast<Control>().Where(c => source.GetRow(c) == row).ToArray();
            var title = cells.FirstOrDefault(c => Equals(c.Tag,"SettingsSection"));
            if (title != null) {
                var page = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20,12,16,12), Visible = false };
                target = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 0 };
                target.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180)); target.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                page.Controls.Add(target); content.Controls.Add(page);
                var button = new RoundedButton { Text = title.Text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10,0,0,0), Margin = new Padding(0,2,0,2), Cursor = Cursors.Hand, Font = AppFonts.Semibold(9) };
                int index = sections.Count; button.Click += (_,_) => SelectSection(index);
                nav.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); nav.Controls.Add(button,0,nav.RowCount++);
                sections.Add((button,page));
            }
            if (target == null) continue;
            int targetRow = target.RowCount++;
            var style = row < source.RowStyles.Count ? source.RowStyles[row] : new RowStyle(SizeType.Absolute,60);
            target.RowStyles.Add(new RowStyle(style.SizeType,style.Height));
            foreach (var cell in cells) {
                int column = source.GetColumn(cell), span = source.GetColumnSpan(cell);
                source.Controls.Remove(cell); target.Controls.Add(cell,column,targetRow); target.SetColumnSpan(cell,span);
            }
        }
        source.Dispose(); if (sections.Count > 0) SelectSection(0);
    }
    internal void SelectSection(int index)
    {
        SelectedIndex = index;
        for (int i = 0; i < sections.Count; i++) {
            bool active = i == index; var item = sections[i]; item.Page.Visible = active;
            if (active) item.Page.BringToFront();
            item.Button.BackColor = active ? DarkTheme.SurfaceHover : DarkTheme.Window;
            item.Button.ForeColor = active ? DarkTheme.Accent : DarkTheme.Muted;
        }
    }
}
