using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SimpleAimOverlay;

internal sealed class KeyBindingButton : RoundedButton
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Binding { get => Text; set => Text = value; }
    public event EventHandler? BindingChanged;
    public KeyBindingButton() { FlatStyle=FlatStyle.Flat; BackColor=DarkTheme.Input; ForeColor=DarkTheme.Text; TextAlign=ContentAlignment.MiddleLeft; Padding=new Padding(12,0,0,0); }
    protected override void OnClick(EventArgs e) {
        base.OnClick(e);
        using var dialog=new CaptureDialog(Binding);
        if(dialog.ShowDialog(FindForm())==DialogResult.OK){Binding=dialog.Binding;BindingChanged?.Invoke(this,EventArgs.Empty);}
    }
    internal static Keys Normalize(Keys key) => key switch { Keys.LControlKey or Keys.RControlKey => Keys.ControlKey, Keys.LShiftKey or Keys.RShiftKey => Keys.ShiftKey, Keys.LMenu or Keys.RMenu => Keys.Menu, _=>key };
    internal static string Label(Keys key) => Normalize(key) switch { Keys.ControlKey=>"Ctrl",Keys.ShiftKey=>"Shift",Keys.Menu=>"Alt",Keys.LButton=>"Left Mouse",Keys.RButton=>"Right Mouse",Keys.MButton=>"Middle Mouse",Keys.XButton1=>"Mouse X1",Keys.XButton2=>"Mouse X2",_=>key.ToString() };
    internal static Keys[] Parse(string binding) => binding.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(s=>s switch {"Ctrl"=>Keys.ControlKey,"Shift"=>Keys.ShiftKey,"Alt"=>Keys.Menu,"Right Mouse"=>Keys.RButton,"Left Mouse"=>Keys.LButton,"Middle Mouse"=>Keys.MButton,"Mouse X1"=>Keys.XButton1,"Mouse X2"=>Keys.XButton2,_=>Enum.TryParse<Keys>(s,out var k)?Normalize(k):Keys.None}).Distinct().ToArray();
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    internal static bool IsDown(string binding) { var keys=Parse(binding);return keys.Length is >=1 and <=3 && keys.All(k=>k!=Keys.None&&(GetAsyncKeyState((int)k)&0x8000)!=0); }
    private sealed class CaptureDialog : StyledDialog {
        readonly HashSet<Keys> held=[];
        readonly Label value=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,BackColor=DarkTheme.Input};
        public string Binding {get;private set;}
        public CaptureDialog(string binding){
            Binding=binding; Text="Set key binding";ClientSize=new Size(490,240);KeyPreview=true;
            Controls.Add(value);value.Text=binding;
            Controls.Add(new Label{Text="Press up to 3 keys together. For mouse buttons, click below.\nClick Save to use the new binding.",Dock=DockStyle.Top,Height=60});
            var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=46,FlowDirection=FlowDirection.RightToLeft};
            actions.Controls.Add(ActionButton("Cancel",DialogResult.Cancel));actions.Controls.Add(ActionButton("Save",DialogResult.OK));Controls.Add(actions);
            KeyDown+=(_,e)=>{if(held.Count==0)Binding="";held.Add(Normalize(e.KeyCode));Record();e.SuppressKeyPress=true;};
            KeyUp+=(_,e)=>{held.Remove(Normalize(e.KeyCode));e.SuppressKeyPress=true;};
            value.MouseDown+=(_,e)=>{held.Clear();if(ModifierKeys.HasFlag(Keys.Control))held.Add(Keys.ControlKey);if(ModifierKeys.HasFlag(Keys.Shift))held.Add(Keys.ShiftKey);if(ModifierKeys.HasFlag(Keys.Alt))held.Add(Keys.Menu);held.Add(e.Button switch{MouseButtons.Right=>Keys.RButton,MouseButtons.Middle=>Keys.MButton,MouseButtons.XButton1=>Keys.XButton1,MouseButtons.XButton2=>Keys.XButton2,_=>Keys.LButton});Record();};
            value.MouseUp+=(_,_)=>held.Clear();
        }
        void Record(){if(held.Count<=3){Binding=string.Join(" + ",held.Select(Label));value.Text=Binding;}else value.Text="Use no more than 3 keys.";}
    }
}
