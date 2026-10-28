using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
namespace SimpleAimOverlay;

internal class StyledDialog : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    public StyledDialog() {
        using var iconStream = GetType().Assembly.GetManifestResourceStream("SimpleAimOverlay.Assets.simple-aim-overlay.ico") ?? GetType().Assembly.GetManifestResourceStream("app.ico"); if (iconStream != null) Icon = new Icon(iconStream);
        BackColor = Color.FromArgb(15,18,23); ForeColor = Color.FromArgb(230,235,242);
        Font = new Font("Segoe UI",10); StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox=false; MaximizeBox=false;
        ShowInTaskbar=false; Padding=new Padding(24);
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); int dark=1; DwmSetWindowAttribute(Handle,20,ref dark,4); }
    protected Button ActionButton(string text, DialogResult result) {
        var b=new Button { Text=text, DialogResult=result, Width=110, Height=36, FlatStyle=FlatStyle.Flat, BackColor=Color.FromArgb(34,43,55), ForeColor=ForeColor };
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
internal sealed class DarkColorPicker:StyledDialog
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color Color {get;set;}=Color.White;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool FullOpen {get;set;}
    protected override void OnLoad(EventArgs e) {
        base.OnLoad(e);Text="Choose color";ClientSize=new Size(430,330);
        var preview=new Panel {Dock=DockStyle.Top,Height=48,BackColor=Color};
        var grid=new FlowLayoutPanel {Dock=DockStyle.Top,Height=70};
        foreach(var c in new[]{System.Drawing.Color.White,System.Drawing.Color.Lime,System.Drawing.Color.Cyan,System.Drawing.Color.Magenta,System.Drawing.Color.Red,System.Drawing.Color.Yellow,System.Drawing.Color.Black}) {
            var sw=new Button {BackColor=c,Width=45,Height=35,FlatStyle=FlatStyle.Flat};sw.Click+=(_,_)=>{Color=c;preview.BackColor=c;};grid.Controls.Add(sw);
        }
        var hex=new TextBox {Text=ColorTranslator.ToHtml(Color),Dock=DockStyle.Top,BackColor=BackColor,ForeColor=ForeColor,BorderStyle=BorderStyle.FixedSingle};
        var sliders=new FlowLayoutPanel {Dock=DockStyle.Top,Height=110,FlowDirection=FlowDirection.TopDown};
        var channels=new NumericUpDown[3];
        for(int i=0;i<3;i++) {int index=i;var row=new FlowLayoutPanel {Width=370,Height=32};row.Controls.Add(new Label {Text=new[]{"Red","Green","Blue"}[i],Width=100});var n=new NumericUpDown {Minimum=0,Maximum=255,Value=new[]{Color.R,Color.G,Color.B}[i],BackColor=BackColor,ForeColor=ForeColor,Width=200};channels[i]=n;row.Controls.Add(n);sliders.Controls.Add(row);n.ValueChanged+=(_,_)=>{if(channels.All(x=>x!=null)){Color=System.Drawing.Color.FromArgb((int)channels[0].Value,(int)channels[1].Value,(int)channels[2].Value);preview.BackColor=Color;hex.Text=ColorTranslator.ToHtml(Color);}};}
        var actions=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=40,FlowDirection=FlowDirection.RightToLeft};
        var ok=ActionButton("Apply",DialogResult.None);ok.Click+=(_,_)=>{try{Color=ColorTranslator.FromHtml(hex.Text);DialogResult=DialogResult.OK;}catch{hex.BackColor=System.Drawing.Color.DarkRed;}};actions.Controls.Add(ok);var cancel=ActionButton("Cancel",DialogResult.Cancel);actions.Controls.Add(cancel);AcceptButton=ok;CancelButton=cancel;
        foreach(Control c in new Control[]{sliders,hex,grid,preview,actions})Controls.Add(c);
        foreach(Button b in grid.Controls)b.Click+=(_,_)=>hex.Text=ColorTranslator.ToHtml(Color);
    }
}


