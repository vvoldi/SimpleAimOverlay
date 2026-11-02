using System.Text.Json;
namespace SimpleAimOverlay;

internal sealed class AimPreview : Panel
{
    private AimSettings target=new(), from=new();
    private Color color=Color.LimeGreen, outline=Color.Black, fromColor, fromOutline;
    private readonly System.Diagnostics.Stopwatch clock=new();
    private readonly System.Windows.Forms.Timer timer=new(){Interval=16};
    private bool initialized;
    internal AimPreview(){DoubleBuffered=true;ResizeRedraw=true;timer.Tick+=(_,_)=>{if(clock.ElapsedMilliseconds>=120)timer.Stop();Invalidate();};}
    private double Progress => !timer.Enabled ? 1 : 1-Math.Pow(1-Math.Min(1,clock.Elapsed.TotalMilliseconds/120),3);
    internal void UpdateTarget(AimSettings settings,Color nextColor,Color nextOutline)
    {
        double t=Progress;from=Frame(t);fromColor=Blend(fromColor,color,t);fromOutline=Blend(fromOutline,outline,t);
        target=Clone(settings);color=nextColor;outline=nextOutline;
        if(initialized && Visible && UiMotion.Enabled){clock.Restart();timer.Start();}
        else {from=Clone(target);fromColor=color;fromOutline=outline;timer.Stop();}
        initialized=true;Invalidate();
    }
    private static AimSettings Clone(AimSettings value)=>JsonSerializer.Deserialize<AimSettings>(JsonSerializer.Serialize(value))!;
    private AimSettings Frame(double t)
    {
        var frame=Clone(target);
        int L(int a,int b)=>(int)Math.Round(a+(b-a)*t);
        frame.Size=L(from.Size,target.Size);frame.Gap=L(from.Gap,target.Gap);frame.ImageScale=L(from.ImageScale,target.ImageScale);
        frame.Thickness=(float)(from.Thickness+(target.Thickness-from.Thickness)*t);
        frame.OutlineWidth=(float)(from.OutlineWidth+(target.OutlineWidth-from.OutlineWidth)*t);
        frame.Opacity=L(from.Opacity,target.Opacity);frame.OutlineOpacity=L(from.OutlineOpacity,target.OutlineOpacity);
        return frame;
    }
    private static Color Blend(Color a,Color b,double t)=>Color.FromArgb((int)(a.A+(b.A-a.A)*t),(int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);double t=Progress;AimRenderer.Draw(e.Graphics,ClientRectangle,Frame(t),Blend(fromColor,color,t),Blend(fromOutline,outline,t),true);}
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
