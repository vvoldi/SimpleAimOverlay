using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SimpleAimOverlay;

internal class RoundedButton : Button
{
    private bool hovered;
    public RoundedButton(){FlatStyle=FlatStyle.Flat;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    public override Size GetPreferredSize(Size proposedSize) {
        var size=base.GetPreferredSize(proposedSize);
        int width=TextRenderer.MeasureText(UiText.T(Text),Font).Width+Padding.Horizontal+12+(Image==null?0:Image.Width+6);
        return new Size(Math.Max(size.Width,width),size.Height);
    }
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hovered=true;Invalidate();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hovered=false;Invalidate();}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(Parent?.BackColor??BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        Color fillColor=hovered?ControlPaint.Light(BackColor,.12f):BackColor;
        using var brush=new SolidBrush(fillColor);using var path=RoundedUi.Path(new Rectangle(0,0,Width-1,Height-1),8);e.Graphics.FillPath(brush,path);
        if(Focused&&FindForm()?.ContainsFocus==true){using var pen=new Pen(Color.FromArgb(69,220,153));e.Graphics.DrawPath(pen,path);}
        int imageWidth=Image?.Width??0;
        string caption=UiText.T(Text);
        int textWidth=TextRenderer.MeasureText(caption,Font).Width;
        bool left=TextAlign is ContentAlignment.MiddleLeft or ContentAlignment.TopLeft or ContentAlignment.BottomLeft;
        int x=left?Math.Max(12,Padding.Left):Math.Max(6,(Width-textWidth-(imageWidth>0?imageWidth+6:0))/2);
        if(Image!=null){e.Graphics.DrawImage(Image,x,(Height-Image.Height)/2);x+=imageWidth+6;}
        TextRenderer.DrawText(e.Graphics,caption,Font,new Rectangle(x,0,Math.Max(0,Width-x-6),Height),Enabled?ForeColor:Color.Gray,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
}

internal static class RoundedUi
{
    private static readonly ConditionalWeakTable<Control,object> attached=new();
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
    [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd,string subApp,string? subId);
    internal static GraphicsPath Path(Rectangle bounds,int radius=7) {
        var path=new GraphicsPath();int d=Math.Max(1,Math.Min(radius*2,Math.Min(bounds.Width,bounds.Height)));
        path.AddArc(bounds.Left,bounds.Top,d,d,180,90);path.AddArc(bounds.Right-d,bounds.Top,d,d,270,90);path.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);path.AddArc(bounds.Left,bounds.Bottom-d,d,d,90,90);path.CloseFigure();return path;
    }
    internal static void Apply(Control root) {
        if(attached.TryGetValue(root,out _))return;attached.Add(root,new object());
        if(root is Form form){int rounded=2;if(form.IsHandleCreated)DwmSetWindowAttribute(form.Handle,33,ref rounded,4);}
        if(root is ScrollableControl){root.HandleCreated+=(_,_)=>SetWindowTheme(root.Handle,"DarkMode_Explorer",null);if(root.IsHandleCreated)SetWindowTheme(root.Handle,"DarkMode_Explorer",null);}
        if(root is Button || root is TextBoxBase || root is NumericUpDown || root.GetType().Name is "DarkComboBox" or "DarkNumberBox" || root.GetType()==typeof(Panel)) {
            void Resize(){if(root.Width<2||root.Height<2)return;using var path=Path(new Rectangle(0,0,root.Width,root.Height));var old=root.Region;root.Region=new Region(path);old?.Dispose();}
            root.Resize+=(_,_)=>Resize();Resize();
            if(root is Button button){button.FlatAppearance.BorderSize=0;}
            if(root is TextBox text)text.BorderStyle=BorderStyle.None;
        }
        foreach(Control child in root.Controls)Apply(child);
        root.ControlAdded+=(_,e)=>{if(e.Control!=null)Apply(e.Control);};
    }
}
