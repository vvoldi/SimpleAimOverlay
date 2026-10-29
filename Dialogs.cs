using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
namespace SimpleAimOverlay;

internal class StyledDialog : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    public StyledDialog() {
        using var iconStream = GetType().Assembly.GetManifestResourceStream("SimpleAimOverlay.Assets.simple-aim-overlay.ico") ?? GetType().Assembly.GetManifestResourceStream("app.ico"); if (iconStream != null) Icon = new Icon(iconStream);
        BackColor = DarkTheme.Window; ForeColor = DarkTheme.Text;
        AppFonts.Initialize(); Font = AppFonts.Regular(10); StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox=false; MaximizeBox=false;
        ShowInTaskbar=false; Padding=new Padding(24);
    }
    protected override void OnShown(EventArgs e) {
        UiText.Apply(this); RoundedUi.Apply(this);
        // CenterParent is ignored by modeless Show(owner), and setup dialogs
        // often have no owner at all. Position explicitly after DPI/layout.
        var owner=Owner;
        bool hasOwner=owner!=null&&owner.Visible&&owner.WindowState!=FormWindowState.Minimized;
        Rectangle work=hasOwner?Screen.FromControl(owner!).WorkingArea:Screen.FromPoint(Cursor.Position).WorkingArea;
        Rectangle anchor=hasOwner?owner!.Bounds:work;
        Location=CenteredLocation(Size,anchor,work);
        base.OnShown(e);
    }
    internal static Point CenteredLocation(Size size,Rectangle anchor,Rectangle work) => new(
        Math.Clamp(anchor.Left+(anchor.Width-size.Width)/2,work.Left,Math.Max(work.Left,work.Right-size.Width)),
        Math.Clamp(anchor.Top+(anchor.Height-size.Height)/2,work.Top,Math.Max(work.Top,work.Bottom-size.Height)));
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); DarkTheme.Caption(this); }
    protected Button ActionButton(string text, DialogResult result) {
        var b=new RoundedButton { Text=text, DialogResult=result, Width=110, Height=36, FlatStyle=FlatStyle.Flat, BackColor=DarkTheme.SurfaceHover, ForeColor=ForeColor };
        b.FlatAppearance.BorderSize=0; return b;
    }
}
internal static class DarkMessage
{
    public static DialogResult Show(string text,string title,MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None,MessageBoxDefaultButton defaultButton=MessageBoxDefaultButton.Button1) => Show(null,text,title,buttons,icon,defaultButton);
    public static DialogResult Show(IWin32Window? owner,string text,string title,MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None,MessageBoxDefaultButton defaultButton=MessageBoxDefaultButton.Button1) {
        using var dialog=new MessageDialog(text,title,buttons,defaultButton);
        return owner==null?dialog.ShowDialog():dialog.ShowDialog(owner);
    }
    sealed class MessageDialog:StyledDialog {
        public MessageDialog(string text,string title,MessageBoxButtons buttons,MessageBoxDefaultButton defaultButton) {
            Text=title; ClientSize=new Size(520,320);
            var label=new Label {Text=text,Dock=DockStyle.Fill,AutoSize=false};
            var row=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=50,FlowDirection=FlowDirection.RightToLeft};
            var yes=ActionButton(buttons==MessageBoxButtons.YesNo?"Continue":"OK",buttons==MessageBoxButtons.YesNo?DialogResult.Yes:DialogResult.OK);
            if(buttons==MessageBoxButtons.YesNo) {var no=ActionButton("Cancel",DialogResult.No);row.Controls.Add(no);CancelButton=no;if(defaultButton==MessageBoxDefaultButton.Button2)AcceptButton=no;}
            row.Controls.Add(yes);AcceptButton??=yes;Controls.Add(label);Controls.Add(row);
        }
    }
}

internal sealed class DarkColorPicker : StyledDialog
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color Color { get; set; } = Color.White;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool FullOpen { get; set; }
    private double hue, saturation, brightness;
    private bool updating;
    private readonly NumericUpDown[] channels = new NumericUpDown[7];
    private readonly TextBox hex = new();
    private readonly Panel current = new(){Tag="PreserveColor"};
    private ColorSurface field = null!, strip = null!;
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e); Text="Choose color"; ClientSize=new Size(510,470);
        Color original=Color;
        field=new ColorSurface(false,this){Bounds=new Rectangle(24,24,300,280)};
        strip=new ColorSurface(true,this){Bounds=new Rectangle(338,24,26,280)};
        Controls.Add(field); Controls.Add(strip);
        Controls.Add(new Label{Text="Current",Bounds=new Rectangle(382,24,104,24)});
        current.Bounds=new Rectangle(382,50,104,64);Controls.Add(current);
        Controls.Add(new Label{Text="Original",Bounds=new Rectangle(382,132,104,24)});
        var previous=new Panel{Bounds=new Rectangle(382,158,104,64),BackColor=original,Cursor=Cursors.Hand,Tag="PreserveColor"};
        previous.Click+=(_,_)=>SetColor(original);Controls.Add(previous);
        string[] labels=["R","G","B","A","H","S %","V %"];
        for(int i=0;i<channels.Length;i++) {
            int index=i,x=24+(i%4)*116,y=318+(i/4)*38;
            Controls.Add(new Label{Text=labels[i],Bounds=new Rectangle(x,y+4,32,24)});
            var input=new NumericUpDown{Bounds=new Rectangle(x+33,y,72,28),Minimum=0,Maximum=i==4?359:i>4?100:255,BackColor=BackColor,ForeColor=ForeColor,BorderStyle=BorderStyle.FixedSingle};
            channels[i]=input;Controls.Add(input);
            input.ValueChanged+=(_,_)=>{
                if(updating)return;
                if(index<4)SetColor(System.Drawing.Color.FromArgb((int)channels[3].Value,(int)channels[0].Value,(int)channels[1].Value,(int)channels[2].Value));
                else {hue=(double)channels[4].Value;saturation=(double)channels[5].Value/100;brightness=(double)channels[6].Value/100;UpdateColor();}
            };
        }
        hex.SetBounds(24,400,218,28);hex.BackColor=BackColor;hex.ForeColor=ForeColor;hex.BorderStyle=BorderStyle.FixedSingle;
        hex.TextChanged+=(_,_)=>{
            if(updating)return;
            string value=hex.Text.Trim().TrimStart('#');
            if((value.Length==6||value.Length==8)&&uint.TryParse(value,System.Globalization.NumberStyles.HexNumber,null,out uint argb))
                SetColor(System.Drawing.Color.FromArgb(unchecked((int)(value.Length==6?argb|0xff000000:argb))),false);
        };
        Controls.Add(hex);
        var apply=ActionButton("Apply",DialogResult.OK);apply.SetBounds(376,414,110,36);
        var cancel=ActionButton("Cancel",DialogResult.Cancel);cancel.SetBounds(256,414,110,36);
        Controls.Add(apply);Controls.Add(cancel);AcceptButton=apply;CancelButton=cancel;SetColor(original);
    }
    private void SetColor(Color color,bool updateHex=true) {
        Color=color;hue=color.GetHue();
        double max=Math.Max(color.R,Math.Max(color.G,color.B)),min=Math.Min(color.R,Math.Min(color.G,color.B));
        saturation=max==0?0:(max-min)/max;brightness=max/255;RefreshValues(updateHex);
    }
    private void UpdateColor(){Color=FromHsv(hue,saturation,brightness,Color.A);RefreshValues(true);}
    private void RefreshValues(bool updateHex) {
        updating=true;
        int[] values=[Color.R,Color.G,Color.B,Color.A,Math.Min(359,(int)Math.Round(hue)),(int)Math.Round(saturation*100),(int)Math.Round(brightness*100)];
        for(int i=0;i<values.Length;i++)channels[i].Value=values[i];
        if(updateHex)hex.Text=$"#{Color.A:X2}{Color.R:X2}{Color.G:X2}{Color.B:X2}";
        current.BackColor=Color;field.Invalidate();strip.Invalidate();updating=false;
    }
    internal static Color FromHsv(double h,double s,double v,int alpha=255) {
        double c=v*s,x=c*(1-Math.Abs(h/60%2-1)),m=v-c;
        (double r,double g,double b)=h switch{<60=>(c,x,0d),<120=>(x,c,0d),<180=>(0d,c,x),<240=>(0d,x,c),<300=>(x,0d,c),_=>(c,0d,x)};
        return System.Drawing.Color.FromArgb(alpha,(int)Math.Round((r+m)*255),(int)Math.Round((g+m)*255),(int)Math.Round((b+m)*255));
    }
    private sealed class ColorSurface : Control {
        private readonly bool isHue; private readonly DarkColorPicker owner;
        public ColorSurface(bool isHue,DarkColorPicker owner){this.isHue=isHue;this.owner=owner;Cursor=Cursors.Cross;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnPaint(PaintEventArgs e) {
            if(isHue){
                for(int y=0;y<Height;y++){using var pen=new Pen(FromHsv(y*359d/Math.Max(1,Height-1),1,1));e.Graphics.DrawLine(pen,0,y,Width,y);}
                int marker=(int)(owner.hue/359*(Height-1));
                using var black=new Pen(System.Drawing.Color.Black,4);using var white=new Pen(System.Drawing.Color.White,2);
                e.Graphics.DrawLine(black,0,marker,Width,marker);e.Graphics.DrawLine(white,0,marker,Width,marker);
            }else{
                using var horizontal=new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle,System.Drawing.Color.White,FromHsv(owner.hue,1,1),0f);e.Graphics.FillRectangle(horizontal,ClientRectangle);
                using var vertical=new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle,System.Drawing.Color.FromArgb(0,0,0,0),System.Drawing.Color.Black,90f);e.Graphics.FillRectangle(vertical,ClientRectangle);
                float x=(float)owner.saturation*(Width-1),y=(float)(1-owner.brightness)*(Height-1);
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var black=new Pen(System.Drawing.Color.Black,4);using var white=new Pen(System.Drawing.Color.White,2);
                e.Graphics.DrawEllipse(black,x-5,y-5,10,10);e.Graphics.DrawEllipse(white,x-5,y-5,10,10);
            }
        }
        private void Pick(MouseEventArgs e){
            if(isHue)owner.hue=Math.Clamp(e.Y/(double)Math.Max(1,Height-1),0,1)*359;
            else{owner.saturation=Math.Clamp(e.X/(double)Math.Max(1,Width-1),0,1);owner.brightness=1-Math.Clamp(e.Y/(double)Math.Max(1,Height-1),0,1);}
            owner.UpdateColor();
        }
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Capture=true;Pick(e);}}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture)Pick(e);}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);Capture=false;}
    }
}
