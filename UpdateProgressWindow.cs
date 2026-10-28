using System.Diagnostics;

namespace SimpleAimOverlay;

internal sealed class UpdateProgressWindow : StyledDialog
{
    private readonly Label headline = new() { Dock = DockStyle.Top, Height = 36, Text = "Connecting to GitHub…" };
    private readonly Label detail = new() { Dock = DockStyle.Top, Height = 60, Text = "Preparing your update", AutoSize = false };
    private readonly DownloadBar bar = new() { Dock = DockStyle.Top, Height = 12, Margin = new Padding(0,12,0,12) };
    private readonly Button cancel;
    private bool finished;
    public event Action? CancelRequested;
    public UpdateProgressWindow()
    {
        Text="Updating Simple Aim Overlay";ClientSize=new Size(480,220);ShowInTaskbar=true;
        cancel=ActionButton("Cancel",DialogResult.None);cancel.Dock=DockStyle.Bottom;
        cancel.Click+=(_,_)=>CancelDownload();
        FormClosing+=(_,e)=>{if(!finished){e.Cancel=true;CancelDownload();}};
        Controls.Add(cancel);Controls.Add(detail);Controls.Add(bar);Controls.Add(headline);
    }
    private void CancelDownload(){cancel.Enabled=false;headline.Text="Cancelling…";CancelRequested?.Invoke();}
    public void Report(AppUpdater.DownloadProgress progress)
    {
        if(IsDisposed||!cancel.Enabled)return;
        int percent=(int)(progress.Received*100/Math.Max(1,progress.Total));
        headline.Text=$"Downloading update · {percent}%";bar.Percent=percent;
        double mb=1024d*1024;
        string remaining=progress.BytesPerSecond>0?TimeSpan.FromSeconds(Math.Min(86400,(progress.Total-progress.Received)/progress.BytesPerSecond)).ToString(@"mm\:ss"):"—";
        detail.Text=$"{progress.Received/mb:F1} / {progress.Total/mb:F1} MB\n{progress.BytesPerSecond/mb:F1} MB/s · about {remaining} remaining";
    }
    public void SetStage(string text){if(IsDisposed)return;headline.Text=text;detail.Text="Keep the app open. Your settings will be preserved.";}
    public void Finish(){finished=true;Close();}
    private sealed class DownloadBar : Control
    {
        private int percent;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Percent {get=>percent;set{percent=Math.Clamp(value,0,100);Invalidate();}}
        public DownloadBar(){DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(DarkTheme.Card);using var fill=new SolidBrush(DarkTheme.Accent);e.Graphics.FillRectangle(fill,0,0,Width*percent/100,Height);}
    }
}
