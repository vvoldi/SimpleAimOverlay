namespace SimpleAimOverlay;

internal static class GamePathRow
{
    internal static TableLayoutPanel Create(TextBox input, Button browse)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0,4,0,4) };
        row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
        input.Dock = DockStyle.None; input.Anchor = AnchorStyles.Left|AnchorStyles.Right; input.Margin = new Padding(0,0,8,0);
        browse.AutoSize = false; browse.Padding = Padding.Empty; browse.Margin = Padding.Empty; browse.Dock = DockStyle.Fill;
        row.Controls.Add(input,0,0);row.Controls.Add(browse,1,0);
        return row;
    }
}
