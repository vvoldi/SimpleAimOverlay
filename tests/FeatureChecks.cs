using SimpleAimOverlay;
using System.Drawing;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;

internal static class FeatureChecks
{
    static void Assert(bool value,string reason){if(!value)throw new Exception(reason);}
    internal static void Run(){
        const BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
        byte[] edid=new byte[128];new byte[]{0,255,255,255,255,255,255,0}.CopyTo(edid,0);edid[57]=0xfc;
        System.Text.Encoding.ASCII.GetBytes("My Monitor\n  ").CopyTo(edid,59);
        Assert(DisplayDetails.ReadEdidName(edid)=="My Monitor","Monitor name drops EDID padding and terminator");
        Assert(DisplayDetails.ReadEdidName(new byte[4])==null&&DisplayDetails.ReadEdidName(new byte[128])==null,"Missing monitor metadata has a safe fallback");
        foreach(var screen in Screen.AllScreens) {
            string description=DisplayDetails.Describe(screen,1);
            Assert(!description.Contains('\\')&&description.Contains('×'),"Monitor labels are readable without raw Windows device paths");
            Console.WriteLine("Display: "+description);
        }
        Assert(GameLauncher.ShouldShow(true,false,true,true),"Selected foreground game permits the crosshair");
        Assert(!GameLauncher.ShouldShow(true,false,true,false),"Alt-tab or game exit hides a game-only crosshair");
        Assert(GameLauncher.ShouldShow(true,false,false,false),"Disabling game-only mode allows desktop overlay");
        Assert(!GameLauncher.ShouldShow(false,false,true,true),"Returning to the game does not undo the F8 off preference");
        Assert(!GameLauncher.ShouldShow(true,true,true,true),"Hold-to-hide still works inside the selected game");
        Assert(!GameLauncher.IsForegroundTarget(""),"An empty target cannot match the foreground window");
        using(var overlay=new OverlayWindow()){
            overlay.UpdateAim(new AimSettings{Visible=false},Color.Lime,Color.Black,Screen.PrimaryScreen!);
            Assert(!overlay.Visible&&!overlay.IsHandleCreated,"Starting hidden never opens an empty fullscreen overlay");
        }
        // Configuration buttons must fit even when the available width wraps them.
        foreach(int width in new[]{500,260}){
            using var host=new Form{ClientSize=new Size(width,240)};
            var grid=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2};
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));host.Controls.Add(grid);
            var flow=new FlowLayoutPanel();
            foreach(string text in new[]{"Export","Import"})flow.Controls.Add(new RoundedButton{Text=text,Size=new Size(90,38),Margin=new Padding(0,4,8,4)});
            var addRow=typeof(ControlWindow).GetMethod("AddRow",BindingFlags.NonPublic|BindingFlags.Static)!;
            addRow.Invoke(null,[grid,"Configuration",flow]);
            var next=new Button{Text="Open update logs"};addRow.Invoke(null,[grid,"Diagnostics",next]);
            host.Show();Application.DoEvents();
            foreach(Control button in flow.Controls)Assert(button.Bottom+button.Margin.Bottom<=flow.ClientSize.Height,"Button and bottom margin fit inside configuration row");
            Assert(flow.Bottom<=next.Top,"Following row cannot overlap configuration buttons");
            host.Close();
        }
        var legacy=JsonSerializer.Deserialize<AimSettings>("{\"Thickness\":1}")!;
        Assert(legacy.Thickness==1&&legacy.Hotkey=="F8","Legacy integer configuration remains compatible");
        double previous=0;
        for(int step=10;step<=20;step++){
            var settings=new AimSettings{Thickness=step/10f,Size=32,Gap=7};
            using var image=new Bitmap(128,128);using(var g=Graphics.FromImage(image))AimRenderer.Draw(g,new Rectangle(0,0,128,128),settings,Color.Lime,Color.Black,false);
            double coverage=Enumerable.Range(60,9).Sum(y=>(int)image.GetPixel(78,y).A);
            if(step<20)Assert(image.GetPixel(78,64).A==255,$"Thickness {settings.Thickness} retains an opaque center pixel");
            Assert(coverage>previous,$"Thickness {settings.Thickness} increases pixel coverage");previous=coverage;
        }
        using(var image=new Bitmap(128,128)){
            foreach(Color background in new[]{Color.White,Color.Black}) {
                using(var g=Graphics.FromImage(image)) {
                    g.Clear(background);
                    AimRenderer.Draw(g,new Rectangle(0,0,128,128),new AimSettings{Thickness=1.5f},Color.Lime,Color.Black,false);
                }
                Assert(image.GetPixel(78,64).ToArgb()==Color.Lime.ToArgb(),"Fractional stroke core has the same color on light and dark backgrounds");
            }
            using(var g=Graphics.FromImage(image)) {
                g.Clear(Color.Transparent);
                AimRenderer.Draw(g,new Rectangle(0,0,128,128),new AimSettings{Thickness=1.5f,Opacity=50},Color.Lime,Color.Black,false);
            }
            Assert(image.GetPixel(78,64).A is >=126 and <=128,"Explicit reduced opacity remains respected");
        }
        using(var image=new Bitmap(128,128)){
            var settings=new AimSettings{Thickness=1,Outline=true,OutlineWidth=3,OutlineOpacity=40};
            using(var g=Graphics.FromImage(image))AimRenderer.Draw(g,new Rectangle(0,0,128,128),settings,Color.Lime,Color.Black,false);
            Assert(image.GetPixel(78,64).A==255,"Outline opacity never dims the main stroke");
            Assert(image.GetPixel(78,66).A is >=99 and <=104,"Outline opacity applies to outline pixels");
        }
        var speed=new RecentDownloadSpeed();speed.Sample(1,40_000_000);
        for(int t=2;t<=7;t++)speed.Sample(t,40_000_000+(t-1)*1_000_000);
        Assert(Math.Abs(speed.Sample(8,47_000_000)-1_000_000)<1,"Recent speed excludes the initial burst");
        Assert(KeyBindingButton.Parse("Ctrl + Shift + F8").SequenceEqual(new[]{Keys.ControlKey,Keys.ShiftKey,Keys.F8}),"Three-key binding parses");
        Assert(KeyBindingButton.Parse("Ctrl + Right Mouse").SequenceEqual(new[]{Keys.ControlKey,Keys.RButton}),"Mouse combination parses");
        using(var bitmap=new Bitmap(64,64))using(var canvas=new DrawingCanvas(bitmap))using(var view=new EditorViewport(canvas){Size=new Size(300,300)}){
            view.CreateControl();view.PerformLayout();int events=0;
            view.ZoomRequested+=steps=>{events++;canvas.PixelSize=Math.Clamp(canvas.PixelSize+steps,3,20);};
            view.ZoomWheel(60,new Point(100,100));Assert(events==0,"High resolution partial wheel accumulates");
            view.ZoomWheel(60,new Point(100,100));Assert(canvas.PixelSize==11,"Ctrl wheel changes canvas zoom");
            Assert((int)typeof(EditorViewport).GetField("offsetX",fields)!.GetValue(view)! == 10,"Zoom keeps pixel under pointer");
        }
        using(var editor=new PixelCrosshairEditor(null,Color.Cyan)){
            Assert(!editor.HasUnsavedChanges(),"New blank editor is clean");
            var canvas=(DrawingCanvas)typeof(PixelCrosshairEditor).GetField("canvas",fields)!.GetValue(editor)!;
            canvas.Image.SetPixel(3,3,Color.Cyan);Assert(editor.HasUnsavedChanges(),"Changed pixel prompts before close");
            canvas.Image.SetPixel(3,3,Color.FromArgb(0,0,0,0));Assert(!editor.HasUnsavedChanges(),"Returning to original drawing is clean");
        }
        string file=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".aimconfig");
        try{
            var original=new StoreData{Current=new AimSettings{Thickness=1.7f,OutlineWidth=2.3f,OffsetX=-57,Hotkey="Ctrl + F8"},Images=[]};
            ConfigurationTransfer.Export(file,original);var restored=ConfigurationTransfer.Import(file);
            Assert(restored.Current.Thickness==1.7f&&restored.Current.OffsetX==-57&&restored.Current.Hotkey=="Ctrl + F8"&&restored.Current.OutlineWidth==2.3f,"Configuration export/import preserves settings");
            using var bitmap=new Bitmap(8,8);bitmap.SetPixel(3,4,Color.FromArgb(123,50,100,150));using var memory=new MemoryStream();bitmap.Save(memory,System.Drawing.Imaging.ImageFormat.Png);
            var drawing=new ConfigurationTransfer.Drawing("shared",false,Convert.ToBase64String(memory.ToArray()));using var decoded=ConfigurationTransfer.Decode(drawing);
            Assert(decoded.GetPixel(3,4).ToArgb()==bitmap.GetPixel(3,4).ToArgb(),"Shared drawing preserves RGBA");
        }finally{File.Delete(file);}
        Console.WriteLine("PASS: fractional coverage, outline width/opacity, rolling speed, key chords, anchored zoom, unsaved drawings, portable configuration and drawing RGBA.");
        if(Environment.GetEnvironmentVariable("SAO_RENDER_MAIN")=="1"){
            byte[]? saved=File.Exists(Store.FilePath)?File.ReadAllBytes(Store.FilePath):null;
            try{
                using var form=new ControlWindow();((StoreData)typeof(ControlWindow).GetField("data",fields)!.GetValue(form)!).LaunchWithGame=false;form.Show();Application.DoEvents();form.PerformLayout();RoundedUi.Apply(form);
                var tabs=(DarkTabHost)typeof(ControlWindow).GetField("tabs",fields)!.GetValue(form)!;
                for(int page=0;page<3;page++){
                    typeof(DarkTabHost).GetMethod("SelectPage",fields)!.Invoke(tabs,[page]);form.PerformLayout();
                    using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(0,0,form.Width,form.Height));image.Save(Path.Combine(Path.GetTempPath(),$"SimpleAimOverlay-0113-page{page}.png"));
                }
                typeof(Form).GetMethod("OnFormClosing",fields)!.Invoke(form,[new FormClosingEventArgs(CloseReason.ApplicationExitCall,false)]);
            }finally{if(saved!=null)File.WriteAllBytes(Store.FilePath,saved);else File.Delete(Store.FilePath);}
        }
    }
}
