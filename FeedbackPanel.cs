using System.Diagnostics;

namespace SimpleAimOverlay;

internal sealed class FeedbackPanel : Panel
{
    // Public service address only. GitHub/Turnstile credentials stay on the Worker.
    internal const string Endpoint="https://simple-aim-overlay-feedback.simple-aim-overlay-feedback.workers.dev";
    readonly TextBox description=new(){Multiline=true,AcceptsReturn=true,ScrollBars=ScrollBars.Vertical,MaxLength=2000,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None};
    readonly DarkComboBox kind=new(){Dock=DockStyle.Fill};
    readonly DarkCheckBox diagnostics=new(){Text="Include technical details",Checked=true,Dock=DockStyle.Fill};
    readonly Label status=new(){Dock=DockStyle.Fill,AutoSize=true};
    readonly Func<StoreData> data;
    public FeedbackPanel(Func<StoreData> getData)
    {
        data=getData;BackColor=DarkTheme.Surface;Padding=new Padding(18);AutoScroll=true;
        var grid=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,BackColor=DarkTheme.Surface};
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(grid);
        void Add(Control c,int height){int row=grid.RowCount++;grid.RowStyles.Add(new RowStyle(SizeType.Absolute,height));c.Margin=new Padding(0,0,0,10);grid.Controls.Add(c,0,row);}
        Add(new Label{Text="Report a bug or suggest an improvement",Dock=DockStyle.Fill,ForeColor=DarkTheme.Text,Font=AppFonts.Semibold(12)},40);
        kind.Items.AddRange(["Bug report","Suggestion"]);kind.SelectedIndex=0;Add(kind,48);
        Add(new Label{Text="Describe what happened or what you would like to improve.",Dock=DockStyle.Fill,ForeColor=DarkTheme.Muted},44);
        description.BackColor=DarkTheme.Input;description.ForeColor=DarkTheme.Text;Add(description,165);
        Add(diagnostics,42);
        Add(new Label{Text="Automatically adds app/Windows versions, display sizes and crosshair settings. No account name, file paths, drawings or logs.",Dock=DockStyle.Fill,ForeColor=DarkTheme.Muted},68);
        Add(new Label{Text="Your report will be public on GitHub. No GitHub account needed. Review and confirm in your browser before sending.",Dock=DockStyle.Fill,ForeColor=DarkTheme.Muted},66);
        var rowPanel=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};
        Button Button(string text){var b=new RoundedButton{Text=text,AutoSize=true,Height=40,Padding=new Padding(12,6,12,6),BackColor=DarkTheme.SurfaceHover,ForeColor=DarkTheme.Text,Margin=new Padding(0,0,10,0)};rowPanel.Controls.Add(b);return b;}
        Button("Preview report").Click+=(_,_)=>Run(()=>{using var dialog=new FeedbackPreview(Build());dialog.ShowDialog(FindForm());});
        var send=Button("Continue to send");send.BackColor=DarkTheme.Accent;send.ForeColor=DarkTheme.Window;
        send.Click+=(_,_)=>Run(()=>{Process.Start(new ProcessStartInfo(Build().Url(Endpoint)){UseShellExecute=true});status.Text="Finish sending in your browser. Your text stays here.";});
        Add(rowPanel,60);status.ForeColor=DarkTheme.Muted;Add(status,56);
    }
    FeedbackReport Build()=>FeedbackReport.Create(kind.SelectedIndex==1?"suggestion":"bug",description.Text,data(),DeviceDpi,diagnostics.Checked);
    void Run(Action action){try{action();}catch(Exception ex){status.Text=ex is ArgumentException or InvalidOperationException?ex.Message:"Could not open the feedback page. Please try again.";}}
    private sealed class FeedbackPreview : StyledDialog
    {
        public FeedbackPreview(FeedbackReport report){Text="Preview report";ClientSize=new Size(620,520);
            var text=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Text=report.Preview(),BackColor=DarkTheme.Input,ForeColor=DarkTheme.Text,BorderStyle=BorderStyle.None};
            var close=ActionButton("OK",DialogResult.OK);close.Dock=DockStyle.Bottom;close.Height=42;Controls.Add(text);Controls.Add(close);AcceptButton=close;
        }
    }
}
