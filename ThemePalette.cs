using System.Runtime.InteropServices;

namespace SimpleAimOverlay;

internal static class DarkTheme
{
    internal static bool Light { get; private set; }
    private static readonly Color[] dark=[Color.FromArgb(15,18,23),Color.FromArgb(22,27,35),Color.FromArgb(27,33,43),Color.FromArgb(19,24,32),Color.FromArgb(35,43,55),Color.FromArgb(47,57,71),Color.FromArgb(235,240,246),Color.FromArgb(151,163,180),Color.FromArgb(69,220,153),Color.FromArgb(52,195,133),Color.FromArgb(9,13,19)];
    private static readonly Color[] light=[Color.FromArgb(242,245,248),Color.FromArgb(250,251,253),Color.FromArgb(232,237,242),Color.FromArgb(255,255,255),Color.FromArgb(221,229,237),Color.FromArgb(180,193,205),Color.FromArgb(25,36,47),Color.FromArgb(79,96,112),Color.FromArgb(0,120,80),Color.FromArgb(0,101,68),Color.FromArgb(226,232,238)];
    private static Color At(int i)=>(Light?light:dark)[i];
    public static Color Window=>At(0); public static Color Surface=>At(1); public static Color Card=>At(2);
    public static Color Input=>At(3); public static Color SurfaceHover=>At(4); public static Color Border=>At(5);
    public static Color Text=>At(6); public static Color Muted=>At(7); public static Color Accent=>At(8);
    public static Color AccentHover=>At(9); public static Color Preview=>At(10);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
    [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr h,string name,string? id);
    internal static void Caption(Form form)
    {
        if(!form.IsHandleCreated)return;
        int mode=Light?0:1,background=ColorTranslator.ToWin32(Window),text=ColorTranslator.ToWin32(Text);
        DwmSetWindowAttribute(form.Handle,20,ref mode,4);
        DwmSetWindowAttribute(form.Handle,35,ref background,4);
        DwmSetWindowAttribute(form.Handle,34,ref background,4);
        DwmSetWindowAttribute(form.Handle,36,ref text,4);
    }
    internal static void ScrollTheme(Control control){if(control.IsHandleCreated)SetWindowTheme(control.Handle,Light?"Explorer":"DarkMode_Explorer",null);}
    internal static void Select(bool value,Control root)
    {
        bool changed=Light!=value;Light=value;
        if(changed)Recolor(root,value?dark:light,value?light:dark);
        if(root is Form form)Caption(form);
        root.Invalidate(true);
    }
    private static void Recolor(Control control,Color[] from,Color[] to)
    {
        Color Map(Color c){int i=Array.FindIndex(from,x=>x.ToArgb()==c.ToArgb());return i<0?c:to[i];}
        if(!Equals(control.Tag,"PreserveColor"))control.BackColor=Map(control.BackColor);
        control.ForeColor=Map(control.ForeColor);
        if(control is Button button){button.FlatAppearance.BorderColor=Map(button.FlatAppearance.BorderColor);button.FlatAppearance.MouseOverBackColor=Map(button.FlatAppearance.MouseOverBackColor);if(button.Image!=null)button.Image=RecolorIcon(button.Image,from,to);}
        if(control is ToolStrip strip)foreach(ToolStripItem item in strip.Items){item.ForeColor=Text;item.BackColor=Surface;if(item.Image!=null)item.Image=RecolorIcon(item.Image,from,to);}
        if(control is Form form)Caption(form);
        if(control is ScrollableControl)ScrollTheme(control);
        foreach(Control child in control.Controls)Recolor(child,from,to);
        if(control is Form owner)foreach(Form owned in owner.OwnedForms)Recolor(owned,from,to);
        if(control.ContextMenuStrip!=null)Recolor(control.ContextMenuStrip,from,to);
    }
    private static Image RecolorIcon(Image image,Color[] from,Color[] to)
    {
        var bitmap=new Bitmap(image);
        for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++){
            var c=bitmap.GetPixel(x,y);int i=Array.FindIndex(from,p=>p.R==c.R&&p.G==c.G&&p.B==c.B);
            if(i>=0)bitmap.SetPixel(x,y,Color.FromArgb(c.A,to[i]));
        }
        image.Dispose();return bitmap;
    }
    internal static void RefreshMenu(ContextMenuStrip menu){menu.BackColor=Surface;menu.ForeColor=Text;foreach(ToolStripItem item in menu.Items)item.ForeColor=Text;}
}
