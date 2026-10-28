using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SimpleAimOverlay;

internal sealed class DrawingCanvas : Control
{
    private Bitmap image;
    private Bitmap? strokeBase;
    private bool drawing, eraseStroke;
    private Point start, previous;
    private int pixelSize = 10;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Bitmap Image { get => image; set { image = value; UpdateSize(); NotifyChanged(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int PixelSize { get => pixelSize; set { pixelSize = Math.Clamp(value,2,24); UpdateSize(); Invalidate(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int BrushSize { get; set; } = 1;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Color PenColor { get; set; } = Color.LimeGreen;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Eraser { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool MirrorX { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool MirrorY { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public string Tool { get; set; } = "Pencil";
    public event Action<Bitmap>? StrokeStarted;
    public event Action<Color>? PointerColorChanged;
    public event Action<Color>? ColorPicked;
    public event Action? Changed;
    public event Action<int>? WheelMoved;
    protected override void OnMouseWheel(MouseEventArgs e) { WheelMoved?.Invoke(e.Delta); }
    public DrawingCanvas(Bitmap bitmap) {
        image=bitmap; BackColor=DarkTheme.Card; DoubleBuffered=true; Cursor=Cursors.Cross;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);UpdateSize();
    }
    private void UpdateSize() => Size = new Size(image.Width*pixelSize+1,image.Height*pixelSize+1);
    private void NotifyChanged(){Invalidate();Changed?.Invoke();}
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(DarkTheme.Card); e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;e.Graphics.PixelOffsetMode=PixelOffsetMode.Half;
        using var first=new SolidBrush(DarkTheme.Input);using var second=new SolidBrush(DarkTheme.Surface);
        for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)e.Graphics.FillRectangle(((x+y)&1)==0?first:second,x*PixelSize,y*PixelSize,PixelSize,PixelSize);
        e.Graphics.DrawImage(image,new Rectangle(0,0,image.Width*PixelSize,image.Height*PixelSize));
        if(PixelSize>=5){
            e.Graphics.PixelOffsetMode=PixelOffsetMode.None;
            using var dark=new Pen(Color.FromArgb(220,10,14,20),2);
            using var light=new Pen(Color.FromArgb(170,235,240,246),1);
            for(int x=0;x<=image.Width;x++){int px=x*PixelSize;e.Graphics.DrawLine(dark,px,0,px,image.Height*PixelSize);e.Graphics.DrawLine(light,px,0,px,image.Height*PixelSize);}
            for(int y=0;y<=image.Height;y++){int py=y*PixelSize;e.Graphics.DrawLine(dark,0,py,image.Width*PixelSize,py);e.Graphics.DrawLine(light,0,py,image.Width*PixelSize,py);}
        }
        using var center=new Pen(Color.FromArgb(110,DarkTheme.Accent));e.Graphics.DrawLine(center,Width/2,0,Width/2,Height);e.Graphics.DrawLine(center,0,Height/2,Width,Height/2);
    }
    private Point Pixel(Point p)=>new(Math.Clamp(p.X/PixelSize,0,image.Width-1),Math.Clamp(p.Y/PixelSize,0,image.Height-1));
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);if(e.Button is not(MouseButtons.Left or MouseButtons.Right))return;
        Focus();start=previous=Pixel(e.Location);
        if(Tool=="Eyedropper"&&e.Button==MouseButtons.Left){ColorPicked?.Invoke(image.GetPixel(start.X,start.Y));return;}
        eraseStroke=Eraser||e.Button==MouseButtons.Right;
        StrokeStarted?.Invoke(new Bitmap(image));
        if(Tool=="Fill"&&!eraseStroke){FloodFill(start);NotifyChanged();return;}
        drawing=true;Capture=true;strokeBase?.Dispose();strokeBase=new Bitmap(image);RenderStroke(start);
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);Point p=Pixel(e.Location);PointerColorChanged?.Invoke(image.GetPixel(p.X,p.Y));
        if(drawing)RenderStroke(p);
    }
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(drawing)RenderStroke(Pixel(e.Location));drawing=false;Capture=false;strokeBase?.Dispose();strokeBase=null;}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){drawing=false;strokeBase?.Dispose();strokeBase=null;}}
    private void RenderStroke(Point end) {
        if(eraseStroke||Tool=="Pencil") { Line(previous,end);previous=end; }
        else {
            if(strokeBase!=null){using var g=Graphics.FromImage(image);g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(strokeBase,0,0);}
            int left=Math.Min(start.X,end.X),top=Math.Min(start.Y,end.Y),right=Math.Max(start.X,end.X),bottom=Math.Max(start.Y,end.Y);
            if(Tool=="Line")Line(start,end);
            else if(Tool=="Rectangle"){Line(new(left,top),new(right,top));Line(new(right,top),new(right,bottom));Line(new(right,bottom),new(left,bottom));Line(new(left,bottom),new(left,top));}
            else if(Tool=="Ellipse") {
                if(left==right||top==bottom)Line(start,end);
                else {double rx=(right-left)/2d,ry=(bottom-top)/2d,cx=(right+left)/2d,cy=(bottom+top)/2d;int steps=(int)Math.Ceiling(Math.PI*2*Math.Max(rx,ry)*2);Point last=new(right,(int)Math.Round(cy));for(int i=1;i<=steps;i++){double a=i*Math.PI*2/steps;Point next=new((int)Math.Round(cx+Math.Cos(a)*rx),(int)Math.Round(cy+Math.Sin(a)*ry));Line(last,next);last=next;}}
            }
        }
        NotifyChanged();
    }
    // Bresenham interpolation prevents gaps when the pointer moves quickly.
    private void Line(Point a,Point b) {
        int x=a.X,y=a.Y,dx=Math.Abs(b.X-x),dy=-Math.Abs(b.Y-y),sx=x<b.X?1:-1,sy=y<b.Y?1:-1,err=dx+dy;
        while(true){Stamp(x,y);if(x==b.X&&y==b.Y)break;int twice=2*err;if(twice>=dy){err+=dy;x+=sx;}if(twice<=dx){err+=dx;y+=sy;}}
    }
    private void Stamp(int cx,int cy){int offset=(BrushSize-1)/2;for(int y=cy-offset;y<cy-offset+BrushSize;y++)for(int x=cx-offset;x<cx-offset+BrushSize;x++)PutMirrored(x,y,eraseStroke?Color.Transparent:PenColor);}
    private void PutMirrored(int x,int y,Color color){Put(x,y,color);if(MirrorX)Put(image.Width-1-x,y,color);if(MirrorY)Put(x,image.Height-1-y,color);if(MirrorX&&MirrorY)Put(image.Width-1-x,image.Height-1-y,color);}
    private void Put(int x,int y,Color color){if(x>=0&&y>=0&&x<image.Width&&y<image.Height)image.SetPixel(x,y,color);}
    private void FloodFill(Point point) {
        int target=image.GetPixel(point.X,point.Y).ToArgb();if(target==PenColor.ToArgb())return;
        var seen=new bool[image.Width,image.Height];var queue=new Queue<Point>();var pixels=new List<Point>();queue.Enqueue(point);
        while(queue.TryDequeue(out var p)){if(p.X<0||p.Y<0||p.X>=image.Width||p.Y>=image.Height||seen[p.X,p.Y])continue;seen[p.X,p.Y]=true;if(image.GetPixel(p.X,p.Y).ToArgb()!=target)continue;pixels.Add(p);queue.Enqueue(new(p.X-1,p.Y));queue.Enqueue(new(p.X+1,p.Y));queue.Enqueue(new(p.X,p.Y-1));queue.Enqueue(new(p.X,p.Y+1));}
        foreach(var p in pixels)PutMirrored(p.X,p.Y,PenColor);
    }
    protected override void Dispose(bool disposing){if(disposing)strokeBase?.Dispose();base.Dispose(disposing);}
}
