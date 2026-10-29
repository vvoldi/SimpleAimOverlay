using SimpleAimOverlay;

internal sealed class SetupProgress : StyledDialog
{
    readonly Label stage=new(){Dock=DockStyle.Top,Height=55};
    readonly Panel track=new(){Dock=DockStyle.Top,Height=12,BackColor=Color.FromArgb(34,43,55)};
    readonly Panel fill=new(){Dock=DockStyle.Left,Width=0,BackColor=Color.FromArgb(55,213,155)};
    public SetupProgress()
    {
        Text="Updating Simple Aim Overlay";ClientSize=new Size(520,190);
        ShowInTaskbar=true;ControlBox=false;StartPosition=FormStartPosition.CenterScreen;
        track.Controls.Add(fill);
        Controls.Add(new Label{Dock=DockStyle.Bottom,Height=42,Text="Please wait. The app will restart automatically."});
        Controls.Add(track);Controls.Add(stage);
    }
    public void Report(string text,int percent)
    {
        stage.Text=text;fill.Width=track.ClientSize.Width*percent/100;
        Refresh();
    }
}
