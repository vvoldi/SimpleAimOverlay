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
        private readonly Control host; private Bitmap? before;
        internal TransitionFrame(Control host) {
            this.host=host;
            if (!Enabled || !host.Visible || !host.IsHandleCreated || host.Width<1 || host.Height<1) return;
            foreach(var old in (host is TableLayoutPanel && host.Parent!=null ? host.Parent : host).Controls.OfType<FadeLayer>().ToArray()) old.Dispose();
            try { before=new Bitmap(host.Width,host.Height); host.DrawToBitmap(before,host.ClientRectangle); } catch { before?.Dispose();before=null; }
        }
        public void Dispose() {
            if(before==null)return;
            if(host.IsDisposed){before.Dispose();return;}
            Bitmap after;
            try { host.PerformLayout();after=new Bitmap(host.Width,host.Height);host.DrawToBitmap(after,host.ClientRectangle); }
            catch {before.Dispose();before=null;return;}
            bool sibling=host is TableLayoutPanel && host.Parent!=null;
            var layer=new FadeLayer(before,after){Bounds=sibling?host.Bounds:host.ClientRectangle,Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right};
            before=null; (sibling?host.Parent!:host).Controls.Add(layer); layer.BringToFront(); layer.Start();
        }
    }
    private sealed class FadeLayer : Control
    {
        private readonly Bitmap image, after; private readonly Stopwatch clock=new();
        private readonly System.Windows.Forms.Timer timer=new(){Interval=15};
        internal FadeLayer(Bitmap image,Bitmap after){this.image=image;this.after=after;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);TabStop=false;timer.Tick+=(_,_)=>{if(clock.ElapsedMilliseconds>=160)Dispose();else Invalidate();};}
        internal void Start(){clock.Start();timer.Start();}
        protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}base.WndProc(ref m);}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.DrawImage(after,ClientRectangle);using var attributes=new ImageAttributes();var matrix=new ColorMatrix{Matrix33=(float)Math.Max(0,1-clock.Elapsed.TotalMilliseconds/160)};attributes.SetColorMatrix(matrix);e.Graphics.DrawImage(image,ClientRectangle,0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);}
        protected override void Dispose(bool disposing){if(disposing){timer.Dispose();image.Dispose();after.Dispose();}base.Dispose(disposing);}
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
