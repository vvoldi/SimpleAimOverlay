using System.Diagnostics;

namespace SimpleAimOverlay;

internal sealed class UpdateProgressWindow : StyledDialog
{
    private readonly Label headline = new() { Dock = DockStyle.Top, Height = 36, Text = "Connecting to GitHub…" };
    private readonly Label detail = new() { Dock = DockStyle.Top, Height = 60, Text = "Preparing your update", AutoSize = false };
    private readonly DownloadBar bar = new() { Dock = DockStyle.Top, Height = 12, Margin = new Padding(0,12,0,12) };
    private readonly Button cancel;
    private readonly Button install;
    private TaskCompletionSource<bool>? confirmation;
    private bool downloading=true;
    private bool finished;
    public event Action? CancelRequested;
    public UpdateProgressWindow()
    {
        Text="Updating Simple Aim Overlay";ClientSize=new Size(520,260);ShowInTaskbar=true;
        cancel=ActionButton("Cancel",DialogResult.None);cancel.Dock=DockStyle.Bottom;
        install=ActionButton("Update and restart",DialogResult.None);install.Dock=DockStyle.Bottom;install.Height=44;install.Visible=false;
        install.Click+=(_,_)=>ConfirmInstall();
        cancel.Click+=(_,_)=>CancelDownload();
        FormClosing+=(_,e)=>{if(!finished){e.Cancel=true;CancelDownload();}};
        Controls.Add(install);Controls.Add(cancel);Controls.Add(detail);Controls.Add(bar);Controls.Add(headline);
    }
    private void CancelDownload(){cancel.Enabled=false;install.Enabled=false;headline.Text="Cancelling…";confirmation?.TrySetResult(false);CancelRequested?.Invoke();}
    internal Task<bool> WaitForConfirmationAsync()
    {
        downloading=false;bar.Animated=false;bar.Percent=100;
        headline.Text="Ready to update";
        detail.Text="Download verified. Update and restart when you are ready. Your settings will be kept.";
        cancel.Text="Not now";install.Visible=true;
        confirmation=new(TaskCreationOptions.RunContinuationsAsynchronously);
        return confirmation.Task;
    }
    internal void ConfirmInstall(){if(confirmation?.TrySetResult(true)==true){install.Enabled=false;cancel.Enabled=false;}}
    public void Report(AppUpdater.DownloadProgress progress)
    {
        if(IsDisposed||!cancel.Enabled||!downloading)return;
        int percent=(int)(progress.Received*100/Math.Max(1,progress.Total));
        headline.Text=$"Downloading update · {percent}%";bar.Percent=percent;
        double mb=1024d*1024;
        string remaining=progress.BytesPerSecond>0?TimeSpan.FromSeconds(Math.Min(86400,(progress.Total-progress.Received)/progress.BytesPerSecond)).ToString(@"mm\:ss"):"—";
        detail.Text=$"{progress.Received/mb:F1} / {progress.Total/mb:F1} MB\n{progress.BytesPerSecond/mb:F1} MB/s (last 4s) · about {remaining} remaining";
    }
    public void SetStage(string text){if(IsDisposed)return;downloading=false;bar.Animated=true;headline.Text=text;detail.Text="Keep the app open. Your settings will be preserved.";}
    public void Finish(){finished=true;Close();}
    protected override void Dispose(bool disposing){if(disposing)confirmation?.TrySetResult(false);base.Dispose(disposing);}
    private sealed class DownloadBar : Control
    {
        private int percent;
        private readonly System.Windows.Forms.Timer timer=new(){Interval=30};
        private int phase;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool Animated {get=>timer.Enabled;set{timer.Enabled=value;Invalidate();}}
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Percent {get=>percent;set{percent=Math.Clamp(value,0,100);Invalidate();}}
        public DownloadBar(){DoubleBuffered=true;timer.Tick+=(_,_)=>{phase=(phase+6)%Math.Max(1,Width+Width/3);Invalidate();};}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(DarkTheme.Card);using var fill=new SolidBrush(DarkTheme.Accent);e.Graphics.FillRectangle(fill,Animated?phase-Width/3:0,0,Animated?Width/3:Width*percent/100,Height);}
        protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
    }
}
