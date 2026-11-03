namespace SimpleAimOverlay;

// A tooltip must not activate its owner or depend on which application has focus.
internal sealed class HelpPopup : ToolStripDropDown
{
    private readonly HelpTextItem textItem=new(){AutoSize=false,Margin=Padding.Empty};
    internal HelpPopup()
    {
        AutoClose=false;AutoSize=false;Padding=Padding.Empty;Items.Add(textItem);
        DoubleBuffered=true;Font=AppFonts.Regular(9.5f);
        TabStop=false;SetStyle(ControlStyles.Selectable,false);
    }

    protected override CreateParams CreateParams {get {var cp=base.CreateParams;cp.ExStyle|=0x08000000|0x80;return cp;}}
    internal void ShowFor(Control anchor,string text)
    {
        BackColor=DarkTheme.SurfaceHover;ForeColor=DarkTheme.Text;
        var area=Screen.FromControl(anchor).WorkingArea;
        int width=Math.Min(380,Math.Max(100,area.Width-24));
        var measured=TextRenderer.MeasureText(text,Font,new Size(width-24,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
        ClientSize=new Size(Math.Min(width,measured.Width+24),measured.Height+20);
        textItem.Size=ClientSize;textItem.Text=text;textItem.Font=Font;textItem.ForeColor=ForeColor;
        var location=anchor.PointToScreen(new Point(anchor.Width+8,anchor.Height/2));
        if(location.X+Width>area.Right)location.X=anchor.PointToScreen(Point.Empty).X-Width-8;
        Location=new Point(Math.Clamp(location.X,area.Left,Math.Max(area.Left,area.Right-Width)),Math.Clamp(location.Y,area.Top,Math.Max(area.Top,area.Bottom-Height)));
        if(!Visible)Show(Location);
        Invalidate();
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x21){m.Result=new IntPtr(3);return;} // MA_NOACTIVATE
        if(m.Msg==0x84){m.Result=new IntPtr(-1);return;} // HTTRANSPARENT
        base.WndProc(ref m);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        using var pen=new Pen(DarkTheme.Border);e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);

    }
    private sealed class HelpTextItem : ToolStripLabel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(12,10,Width-24,Height-20),ForeColor,TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
        }
    }}
