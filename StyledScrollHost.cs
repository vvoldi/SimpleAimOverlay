using System.Runtime.InteropServices;
namespace SimpleAimOverlay;

// Clip the native vertical bar and provide a themed rail, retaining native wheel/key scrolling.
internal sealed class StyledScrollHost : Panel
{
    private readonly Control content;
    internal Control Content => content;
    private readonly Panel viewport=new(){Dock=DockStyle.Fill};
    private readonly Rail rail;
    private readonly System.Windows.Forms.Timer sync=new(){Interval=80};
    internal StyledScrollHost(Control content)
    {
        this.content=content;DoubleBuffered=true;BackColor=content.BackColor==SystemColors.Control?DarkTheme.Surface:content.BackColor;
        content.BackColor=BackColor;viewport.BackColor=BackColor;
        rail=new Rail(this){Dock=DockStyle.Right,Width=14};Controls.Add(viewport);Controls.Add(rail);
        content.Dock=DockStyle.None;content.Anchor=AnchorStyles.Top|AnchorStyles.Left;
        viewport.Controls.Add(content);viewport.SizeChanged+=(_,_)=>Fit();
        sync.Tick+=(_,_)=>{if(!Visible)return;var range=Range();if(rail.Last!=range){rail.Last=range;rail.Invalidate();}};
        VisibleChanged+=(_,_)=>{if(Visible)sync.Start();else sync.Stop();};Fit();
    }
    private void Fit()=>content.Bounds=new Rectangle(0,0,Math.Max(1,viewport.ClientSize.Width+SystemInformation.VerticalScrollBarWidth),Math.Max(1,viewport.ClientSize.Height));
    private (int Top,int Max,int Page) Range(){if(content is ScrollableControl scroll && !scroll.VerticalScroll.Visible)return(0,0,1);var info=new ScrollInfo{Size=Marshal.SizeOf<ScrollInfo>(),Mask=0x17};if(!content.IsHandleCreated||!GetScrollInfo(content.Handle,1,ref info))return(0,0,1);return(info.Position,Math.Max(0,info.Maximum-(int)info.Page+1),Math.Max(1,(int)info.Page));}
    private void ScrollTo(int value){var range=Range();value=Math.Clamp(value,0,range.Max);if(content is ScrollableControl scroll)scroll.AutoScrollPosition=new Point(0,value);else{SetScrollPos(content.Handle,1,value,true);SendMessage(content.Handle,0x115,new IntPtr((value<<16)|4),IntPtr.Zero);}rail.Invalidate();}
    protected override void Dispose(bool disposing){if(disposing)sync.Dispose();base.Dispose(disposing);}
    private sealed class Rail : Control
    {
        private readonly StyledScrollHost owner;private bool dragging;private int offset;
        internal (int Top,int Max,int Page) Last;
        internal Rail(StyledScrollHost owner){this.owner=owner;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        private Rectangle Grip(){var r=owner.Range();int h=Math.Min(Height,Math.Max(28,(int)((long)Height*r.Page/Math.Max(1,r.Max+r.Page))));return new Rectangle(3,r.Max==0?0:(int)((long)(Height-h)*r.Top/r.Max),8,h);}
        protected override void OnPaint(PaintEventArgs e){bool needed=owner.Range().Max>0;e.Graphics.Clear(needed?ScrollPalette.Track:owner.BackColor);if(!needed)return;using var fill=new SolidBrush(ScrollPalette.Thumb);using var path=RoundedUi.Path(Grip(),4);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;e.Graphics.FillPath(fill,path);}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;var grip=Grip();offset=grip.Contains(e.Location)?e.Y-grip.Top:grip.Height/2;dragging=true;Capture=true;ScrollTo(e.Y);}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)ScrollTo(e.Y);}
        private void ScrollTo(int y){var grip=Grip();owner.ScrollTo((int)((long)Math.Clamp(y-offset,0,Math.Max(1,Height-grip.Height))*owner.Range().Max/Math.Max(1,Height-grip.Height)));}
        protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;base.OnMouseUp(e);}
        protected override void OnMouseWheel(MouseEventArgs e){owner.ScrollTo(owner.Range().Top-Math.Sign(e.Delta)*Math.Max(1,owner.Range().Page/12));base.OnMouseWheel(e);}
    }
    [StructLayout(LayoutKind.Sequential)] private struct ScrollInfo{public int Size,Mask,Minimum,Maximum;public uint Page;public int Position,Track;}
    [DllImport("user32.dll")] private static extern bool GetScrollInfo(IntPtr window,int bar,ref ScrollInfo info);
    [DllImport("user32.dll")] private static extern int SetScrollPos(IntPtr window,int bar,int position,bool redraw);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window,int msg,IntPtr wparam,IntPtr lparam);
}
