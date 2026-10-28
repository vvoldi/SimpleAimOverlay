using System.Drawing.Drawing2D;

namespace SimpleAimOverlay;

// Clip the drawing canvas and paint our own scrollbars instead of native white bars.
internal sealed class EditorViewport : UserControl
{
    private readonly Panel clip = new() { Dock = DockStyle.Fill, BackColor = DarkTheme.Card };
    private readonly DrawingCanvas canvas;
    private readonly Bar vertical, horizontal;
    private int offsetX, offsetY, wheelRemainder, zoomRemainder;
    public event Action<int>? ZoomRequested;
    public EditorViewport(DrawingCanvas canvas)
    {
        this.canvas=canvas;BackColor=DarkTheme.Card;
        vertical=new Bar(this,true){Dock=DockStyle.Right,Width=14};horizontal=new Bar(this,false){Dock=DockStyle.Bottom,Height=14};
        Controls.Add(clip);Controls.Add(vertical);Controls.Add(horizontal);clip.Controls.Add(canvas);
        clip.Resize+=(_,_)=>UpdatePosition();canvas.SizeChanged+=(_,_)=>UpdatePosition();
        canvas.WheelMoved+=ScrollWheel;clip.MouseWheel+=(_,e)=>ScrollWheel(e.Delta);
    }
    private int Max(bool vertical)=>Math.Max(0,(vertical?canvas.Height-clip.Height:canvas.Width-clip.Width));
    private void UpdatePosition(){offsetX=Math.Clamp(offsetX,0,Max(false));offsetY=Math.Clamp(offsetY,0,Max(true));canvas.Location=new Point(-offsetX,-offsetY);vertical.Invalidate();horizontal.Invalidate();}
    private void ScrollWheel(int delta){
        if ((ModifierKeys & Keys.Control) != 0) { ZoomWheel(delta, clip.PointToClient(Cursor.Position)); return; }
        wheelRemainder+=delta;int steps=wheelRemainder/120;wheelRemainder%=120;if((ModifierKeys&Keys.Shift)!=0)offsetX-=steps*24;else offsetY-=steps*24;UpdatePosition();}
    internal void ZoomWheel(int delta, Point anchor) {
        zoomRemainder += delta; int steps = zoomRemainder / 120; zoomRemainder %= 120;
        if (steps == 0) return;
        double pixelX = (offsetX + anchor.X) / (double)canvas.PixelSize, pixelY = (offsetY + anchor.Y) / (double)canvas.PixelSize;
        ZoomRequested?.Invoke(steps);
        offsetX = (int)Math.Round(pixelX * canvas.PixelSize - anchor.X); offsetY = (int)Math.Round(pixelY * canvas.PixelSize - anchor.Y);
        UpdatePosition();
    }
    private sealed class Bar : Control {
        readonly EditorViewport owner;readonly bool vertical;bool dragging;int grab;
        public Bar(EditorViewport owner,bool vertical){this.owner=owner;this.vertical=vertical;BackColor=DarkTheme.Card;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
        int Length=>vertical?Height:Width;
        int View=>vertical?owner.clip.Height:owner.clip.Width;
        int Content=>vertical?owner.canvas.Height:owner.canvas.Width;
        int Thumb=>Math.Min(Length,Math.Max(24,Length*View/Math.Max(1,Content)));
        int Travel=>Math.Max(0,Length-Thumb);
        int Position=>owner.Max(vertical)==0?0:Travel*(vertical?owner.offsetY:owner.offsetX)/owner.Max(vertical);
        protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);if(owner.Max(vertical)==0)return;using var brush=new SolidBrush(dragging?DarkTheme.Accent:DarkTheme.Muted);e.Graphics.FillRectangle(brush,vertical?new Rectangle(4,Position,6,Thumb):new Rectangle(Position,4,Thumb,6));}
        void MoveThumb(int coordinate){int position=Travel==0?0:(int)Math.Round((coordinate-grab)*(double)owner.Max(vertical)/Travel);if(vertical)owner.offsetY=position;else owner.offsetX=position;owner.UpdatePosition();}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;int coordinate=vertical?e.Y:e.X;grab=coordinate>=Position&&coordinate<Position+Thumb?coordinate-Position:Thumb/2;dragging=true;Capture=true;MoveThumb(coordinate);}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)MoveThumb(vertical?e.Y:e.X);}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;Invalidate();}
        protected override void OnMouseWheel(MouseEventArgs e){owner.ScrollWheel(e.Delta);}
    }
}
