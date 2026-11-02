namespace SimpleAimOverlay;

internal static class GamePathRow
{
    internal static TableLayoutPanel Create(TextBox input, Button browse)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0,4,0,4) };
        row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
        var field = new Panel { Dock=DockStyle.Fill, Margin=new Padding(0,0,8,0), BackColor=DarkTheme.Input, Padding=new Padding(10,8,10,8) };
        input.BorderStyle=BorderStyle.None;input.Tag="SquareInput";
        input.Dock = DockStyle.None; input.Anchor = AnchorStyles.Left|AnchorStyles.Right; input.Margin = Padding.Empty;
        field.Controls.Add(input);
        void LayoutInput() {
            input.SetBounds(field.Padding.Left,Math.Max(field.Padding.Top,(field.ClientSize.Height-input.PreferredHeight)/2),Math.Max(1,field.ClientSize.Width-field.Padding.Horizontal),input.PreferredHeight);
        }
        field.Resize+=(_,_)=>LayoutInput();input.FontChanged+=(_,_)=>LayoutInput();
        field.Click+=(_,_)=>input.Focus();
        input.MouseDoubleClick+=(_,e)=>{
            if(e.Button==MouseButtons.Left)input.BeginInvoke(new Action(()=>{if(!input.IsDisposed)input.SelectAll();}));
        };
        var pathTip=new ToolTip {ShowAlways=true,InitialDelay=350,ReshowDelay=100,AutoPopDelay=20000};
        void UpdateTip(){pathTip.SetToolTip(input,input.Text);pathTip.SetToolTip(field,input.Text);}
        input.TextChanged+=(_,_)=>UpdateTip();row.Disposed+=(_,_)=>pathTip.Dispose();UpdateTip();
        browse.AutoSize = false; browse.Padding = Padding.Empty; browse.Margin = Padding.Empty; browse.Dock = DockStyle.Fill;
        row.Controls.Add(field,0,0);row.Controls.Add(browse,1,0);
        return row;
    }
}
