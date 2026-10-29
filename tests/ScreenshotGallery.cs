using SimpleAimOverlay;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

internal static class ScreenshotGallery
{
    internal static void Render(string folder)
    {
        Application.EnableVisualStyles();AppFonts.Initialize();Directory.CreateDirectory(folder);
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        void Save(Form form,string name) {
            Application.DoEvents();form.PerformLayout();
            using var full=new Bitmap(form.Width,form.Height);form.DrawToBitmap(full,new Rectangle(Point.Empty,form.Size));
            Point client=form.PointToScreen(Point.Empty)-new Size(form.Left,form.Top);
            using var content=full.Clone(new Rectangle(client,form.ClientSize),PixelFormat.Format32bppArgb);
            content.Save(Path.Combine(folder,name+".png"));
        }
        var demo=new StoreData{Language="en",GameExecutable="TslGame.exe",Current=new AimSettings{Shape="Cross + Dot",Size=32,Thickness=1.5f,Gap=7,Color="#45DC99",Visible=true}};
        using(var main=new ControlWindow(demo){Size=new Size(1180,980)}) {
            main.Show();
            var tabs=(DarkTabHost)typeof(ControlWindow).GetField("tabs",flags)!.GetValue(main)!;
            Save(main,"crosshair");
            typeof(DarkTabHost).GetMethod("SelectPage",flags)!.Invoke(tabs,[2]);Save(main,"settings");
            var language=(ComboBox)typeof(ControlWindow).GetField("language",flags)!.GetValue(main)!;
            language.SelectedIndex=1;Save(main,"settings-uk");
            language.SelectedIndex=0;
            var theme=(ComboBox)typeof(ControlWindow).GetField("theme",flags)!.GetValue(main)!;
            theme.SelectedIndex=1;Save(main,"settings-light");
            typeof(DarkTabHost).GetMethod("SelectPage",flags)!.Invoke(tabs,[0]);Save(main,"crosshair-light");
            theme.SelectedIndex=0;
            typeof(Form).GetMethod("OnFormClosing",flags)!.Invoke(main,[new FormClosingEventArgs(CloseReason.ApplicationExitCall,false)]);
        }
        UiText.Language="en";
        using(var editor=new PixelCrosshairEditor(null,Color.FromArgb(69,220,153))) {
            var canvas=(DrawingCanvas)typeof(PixelCrosshairEditor).GetField("canvas",flags)!.GetValue(editor)!;
            using(var g=Graphics.FromImage(canvas.Image)) {
                using var fill=new SolidBrush(Color.FromArgb(69,220,153));
                g.FillRectangle(fill,31,16,2,12);g.FillRectangle(fill,31,36,2,12);g.FillRectangle(fill,16,31,12,2);g.FillRectangle(fill,36,31,12,2);g.FillRectangle(fill,31,31,2,2);
            }
            editor.Show();Save(editor,"editor");DarkTheme.Select(true,editor);Save(editor,"editor-light");DarkTheme.Select(false,editor);editor.DialogResult=DialogResult.OK;editor.Close();
        }
        using(var palette=new DarkColorPicker{Color=Color.FromArgb(69,220,153)}) {palette.Show();Save(palette,"color-picker");palette.Close();}
        Console.WriteLine("Screenshot gallery saved to "+folder);
    }
}
