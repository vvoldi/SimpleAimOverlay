namespace SimpleAimOverlay;

internal sealed class StartupContext : ApplicationContext
{
    internal StartupContext()
    {
        var loader = new StartupWindow();
        loader.Shown += async (_,_) => {
            await Task.Yield();
            try {
                var window = new ControlWindow(); MainForm = window;
                window.Show(); loader.Close(); loader.Dispose();
            } catch { loader.Dispose(); ExitThread(); throw; }
        };
        loader.Show();
    }
    private sealed class StartupWindow : Form
    {
        private readonly System.Windows.Forms.Timer timer = new(){Interval=16};
        private readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
        private readonly Font detailFont=AppFonts.Regular(9);
        internal StartupWindow() {
            Text="Simple Aim Overlay"; ClientSize=new Size(340,150); FormBorderStyle=FormBorderStyle.None;
            StartPosition=FormStartPosition.CenterScreen; BackColor=DarkTheme.Window; ForeColor=DarkTheme.Text;
            DoubleBuffered=true; ShowInTaskbar=false; Font=AppFonts.Semibold(12);
            timer.Tick+=(_,_)=>Invalidate();timer.Start();
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var track=new Pen(DarkTheme.Border,3);using var arc=new Pen(DarkTheme.Accent,3);
            e.Graphics.DrawEllipse(track,151,26,36,36);e.Graphics.DrawArc(arc,151,26,36,36,(float)(clock.Elapsed.TotalMilliseconds*.3%360),110);
            TextRenderer.DrawText(e.Graphics,"Simple Aim Overlay",Font,new Rectangle(0,82,Width,28),ForeColor,TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(e.Graphics,UiText.T("Starting…"),detailFont,new Rectangle(0,114,Width,24),DarkTheme.Muted,TextFormatFlags.HorizontalCenter);
        }
        protected override void Dispose(bool disposing){if(disposing){timer.Dispose();detailFont.Dispose();}base.Dispose(disposing);}
    }
}
