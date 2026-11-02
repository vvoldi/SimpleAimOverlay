using System.Diagnostics;
using System.Drawing.Imaging;
namespace SimpleAimOverlay;

internal static class UiMotion
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint action,uint param,out bool value,uint flags);
    internal static bool Enabled => !SystemParametersInfo(0x1042,0,out bool enabled,0) || enabled;
    internal static void FadeIn(Form form)
    {
        if (!Enabled) { form.Opacity = 1; return; }
        var watch = Stopwatch.StartNew(); var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_,_) => {
            if (form.IsDisposed) { timer.Dispose(); return; }
            double t = Math.Min(1,watch.Elapsed.TotalMilliseconds/150);
            form.Opacity = 1-Math.Pow(1-t,3);
            if (t >= 1) timer.Dispose();
        };
        form.Disposed += (_,_) => timer.Dispose(); timer.Start();
    }
    internal static IDisposable Transition(Control host) => new TransitionFrame(host);
    private sealed class TransitionFrame : IDisposable
    {
        private readonly Control host;
        private FadeLayer? layer;
        internal TransitionFrame(Control host)
        {
            this.host=host;
            var form=host.FindForm();
            if(!Enabled || form==null || host==form || !host.Visible || !host.IsHandleCreated || host.Width<1 || host.Height<1)return;
            var bounds=new Rectangle(form.PointToClient(host.PointToScreen(Point.Empty)),host.ClientSize);
            Bitmap? before=null;
            try {
                // Keep the current presentation covered until the replacement layer is painted.
                var old=form.Controls.OfType<FadeLayer>().ToArray();
                before=new Bitmap(host.Width,host.Height);
                var previous=old.FirstOrDefault(x=>x.Bounds==bounds);
                if(previous!=null)previous.DrawToBitmap(before,new Rectangle(Point.Empty,before.Size));
                else host.DrawToBitmap(before,host.ClientRectangle);
                layer=new FadeLayer(before){Bounds=bounds};before=null;
                form.Controls.Add(layer);layer.BringToFront();layer.Update();
                foreach(var item in old)item.Dispose();
            } catch {before?.Dispose();layer?.Dispose();layer=null;}
        }
        public void Dispose()
        {
            if(layer==null)return;
            var current=layer;layer=null;
            if(host.IsDisposed || current.IsDisposed){current.Dispose();return;}
            Bitmap? after=null;
            try {
                host.PerformLayout();
                after=new Bitmap(host.Width,host.Height);host.DrawToBitmap(after,host.ClientRectangle);
                current.Start(after);after=null;
            } catch {after?.Dispose();current.Dispose();}
        }
    }
    private sealed class FadeLayer : Control
    {
        private readonly Bitmap image;
        private Bitmap? after;
        private readonly Stopwatch clock=new();
        private bool finishing;
        private readonly System.Windows.Forms.Timer timer=new(){Interval=15};
        internal FadeLayer(Bitmap image)
        {
            this.image=image;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Opaque,true);
            TabStop=false;timer.Tick+=(_,_)=>Invalidate();
        }
        internal void Start(Bitmap next){after=next;Invalidate();timer.Start();}
        protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}base.WndProc(ref m);}
        protected override void OnPaint(PaintEventArgs e)
        {
            if(after==null){e.Graphics.DrawImageUnscaled(image,0,0);return;}
            // Start timing on the first presented frame, not during layout/capture.
            if(!clock.IsRunning)clock.Start();
            double t=Math.Clamp(clock.Elapsed.TotalMilliseconds/180,0,1);
            e.Graphics.DrawImageUnscaled(after,0,0);
            using var attributes=new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix{Matrix33=(float)(1-t*t*(3-2*t))});
            e.Graphics.DrawImage(image,new Rectangle(Point.Empty,image.Size),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);
            if(t>=1 && !finishing){finishing=true;timer.Stop();BeginInvoke(new Action(()=>{if(!IsDisposed)Dispose();}));}
        }
        protected override void Dispose(bool disposing){if(disposing){timer.Dispose();image.Dispose();after?.Dispose();}base.Dispose(disposing);}
    }
}

internal sealed class BufferedPanel : Panel
{
    internal BufferedPanel(){DoubleBuffered=true;ResizeRedraw=true;}
}
internal sealed class ValueLabel : Label
{
    internal ValueLabel(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AutoSize=false;}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,ForeColor,TextFormatFlags.Right|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);}
}
