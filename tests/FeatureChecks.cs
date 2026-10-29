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
        var feedbackData=new StoreData{GameExecutable=@"C:\Users\PRIVATE_USER\SecretGame\game.exe",Current=new AimSettings{ImagePath=@"C:\Users\PRIVATE_USER\drawing.png",Monitor="PRIVATE_MONITOR",Shape="Image"}};
        var feedback=FeedbackReport.Create("bug","Приціл зникає після перемикання.",feedbackData,144,true);
        string feedbackJson=JsonSerializer.Serialize(feedback);
        Assert(!feedbackJson.Contains("PRIVATE_")&&!feedbackJson.Contains("drawing.png")&&!feedbackJson.Contains("game.exe"),"Feedback must omit paths and identifying data");
        Assert(feedback.Diagnostics["UI scale"]=="150%","Feedback collects current UI scale");
        Assert(FeedbackReport.Create("suggestion","Please add another shape",feedbackData,96,false).Diagnostics.Count==0,"Technical details can be excluded");
        var feedbackUrl=new Uri(feedback.Url("https://feedback.example"));
        string encoded=feedbackUrl.Fragment[1..].Replace('-','+').Replace('_','/');encoded=encoded.PadRight((encoded.Length+3)/4*4,'=');
        Assert(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded)).Contains("diagnostics"),"Feedback payload survives UTF-8 URL fragment encoding");
        Assert(feedbackUrl.Query=="","Feedback is not placed in a server query string");
        var work=new Rectangle(-1920,40,1920,1040);
        Assert(StyledDialog.CenteredLocation(new Size(520,320),work,work)==new Point(-1220,400),"Ownerless dialogs center on the selected monitor, including negative coordinates");
        var edge=StyledDialog.CenteredLocation(new Size(520,320),new Rectangle(-100,900,400,300),work);
        Assert(work.Contains(new Rectangle(edge,new Size(520,320))),"Dialogs stay inside the monitor working area near screen edges");
        using(var owner=new Form{StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(140,160,800,600)})using(var dialog=new StyledDialog{ClientSize=new Size(300,200)}) {
            owner.Show();dialog.Show(owner);Application.DoEvents();
            Assert(dialog.Location==StyledDialog.CenteredLocation(dialog.Size,owner.Bounds,Screen.FromControl(owner).WorkingArea),"Modeless update dialogs center on their owner");
            dialog.Close();owner.Close();
        }
        Assert(new StoreData().Theme=="dark","Existing installations retain the dark theme");
        Assert(JsonSerializer.Deserialize<StoreData>(JsonSerializer.Serialize(new StoreData{Theme="light"}))!.Theme=="light","Theme survives saving and loading");
        using(var root=new Form{BackColor=DarkTheme.Window,ForeColor=DarkTheme.Text}){
            var input=new TextBox{BackColor=DarkTheme.Input,ForeColor=DarkTheme.Text};
            var swatch=new Panel{Tag="PreserveColor",BackColor=DarkTheme.Accent};root.Controls.Add(input);root.Controls.Add(swatch);
            Color original=swatch.BackColor;
            DarkTheme.Select(true,root);
            Assert(input.BackColor==DarkTheme.Input&&input.ForeColor==DarkTheme.Text&&root.BackColor==DarkTheme.Window,"Light theme updates existing controls");
            Assert(swatch.BackColor==original,"Theme preserves chosen artwork colors");
            DarkTheme.Select(false,root);Assert(input.BackColor==DarkTheme.Input&&root.BackColor==DarkTheme.Window,"Theme can switch back without restart");
        }
        using(var progress=new UpdateProgressWindow()) {
            progress.Show();var decision=progress.WaitForConfirmationAsync();Application.DoEvents();
            Assert(!decision.IsCompleted,"Verified download must wait for explicit confirmation");
            progress.ConfirmInstall();Assert(decision.Result,"Install button authorizes restart");progress.Finish();
        }
        using(var progress=new UpdateProgressWindow()) {
            progress.Show();var decision=progress.WaitForConfirmationAsync();progress.Close();
            Assert(decision.IsCompleted&&!decision.Result,"Closing ready window must not authorize installation");progress.Finish();
        }
        using(var owner=new Form())using(var progress=new UpdateProgressWindow()) {
            owner.Show();progress.Show(owner);Application.DoEvents();owner.Close();
            Assert(!owner.IsDisposed,"Reproduce old update failure: unfinished owned progress cancels owner close");
            progress.Finish();owner.Close();
        }
        using(var owner=new Form())using(var progress=new UpdateProgressWindow()) {
            owner.Show();progress.Show(owner);Application.DoEvents();bool cancelled=false;
            progress.CancelRequested+=()=>cancelled=true;
            AppUpdater.CompleteHandoff(progress,()=>owner.Close());Application.DoEvents();
            Assert(owner.IsDisposed&&progress.IsDisposed&&!cancelled,"Update handoff closes progress before owner without cancelling exit");
        }
        Assert(new StoreData().Language=="en","English is the default language");
        Assert(JsonSerializer.Deserialize<StoreData>("{}")!.Language=="en","Older configurations keep English by default");
        Assert(JsonSerializer.Deserialize<StoreData>(JsonSerializer.Serialize(new StoreData{Language="uk"}))!.Language=="uk","Language preference survives configuration save/load");
        using(var panel=new Panel()) {
            var label=new Label{Text="Settings"};var input=new TextBox{Text="Crosshair"};var binding=new KeyBindingButton{Binding="Right Mouse"};
            var combo=new DarkComboBox();combo.Items.Add("Cross");combo.SelectedIndex=0;
            panel.Controls.AddRange([label,input,binding,combo]);
            try {
                UiText.Language="uk";UiText.Apply(panel);
                Assert(label.Text=="Налаштування","Static captions translate immediately");
                Assert(input.Text=="Crosshair"&&binding.Binding=="Right Mouse"&&(string)combo.SelectedItem! == "Cross","User data and saved key/style identifiers stay unchanged");
                label.Text="Checking for updates…";Assert(label.Text=="Перевіряю оновлення…","Live status updates are translated");
                Assert(UiText.T("Crosshair hidden · switch to TslGame.exe").EndsWith("TslGame.exe"),"Dynamic target names are preserved");
                Assert(UiText.T("You're up to date · 0.1.13").EndsWith("0.1.13"),"Dynamic versions are preserved");
                UiText.Language="en";UiText.Apply(panel);Assert(label.Text=="Checking for updates…","Switching back restores the latest English caption");
                if(Environment.GetEnvironmentVariable("SAO_RENDER_EDITOR_UK")=="1") {
                    UiText.Language="uk";using var editor=new PixelCrosshairEditor(null,Color.Lime);editor.Show();Application.DoEvents();
                    using var shot=new Bitmap(editor.Width,editor.Height);editor.DrawToBitmap(shot,new Rectangle(0,0,shot.Width,shot.Height));shot.Save(Path.Combine(Path.GetTempPath(),"SimpleAimOverlay-editor-uk.png"));editor.DialogResult=DialogResult.OK;editor.Close();
                }
            }finally{UiText.Language="en";}
        }
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
        foreach(bool preview in new[]{false,true})foreach(int thickness in new[]{1,2,3,4})foreach(bool outlined in new[]{false,true}){
            var bounds=new Rectangle(10,12,310,465);
            float scale=preview?310/230f:1;
            int width=Math.Max(1,(int)MathF.Round(thickness*scale));
            int cx=bounds.Left+(int)MathF.Floor(bounds.Width/2f+3*scale),cy=bounds.Top+(int)MathF.Floor(bounds.Height/2f-2*scale);
            using var image=new Bitmap(340,500);
            using(var g=Graphics.FromImage(image))AimRenderer.Draw(g,bounds,new AimSettings{Shape="Cross + Dot",Size=180,Thickness=thickness,Gap=5,OffsetX=3,OffsetY=-2,Outline=outlined,OutlineWidth=.5f},Color.Lime,Color.Black,preview);
            // The center lies on a pixel for odd widths and between pixels for even widths.
            int mirrorX=2*cx-(width%2==0?1:0),mirrorY=2*cy-(width%2==0?1:0);
            for(int y=cy-4;y<=cy+3;y++)for(int x=cx-4;x<=cx+3;x++){
                Assert(image.GetPixel(x,y).ToArgb()==image.GetPixel(mirrorX-x,mirrorY-y).ToArgb(),"Dot and outline share the crosshair center in preview and overlay");
            }
            Assert(image.GetPixel(cx,cy).G==255,"Centered dot is visible and opaque");
        }
        foreach(int strokeWidth in new[]{1,2,3,4}) {
            using var crisp=new Bitmap(128,128);
            var settings=new AimSettings{Shape="Cross",Size=18,Thickness=strokeWidth,Gap=0};
            var lime=Color.FromArgb(12,255,0);
            using(var g=Graphics.FromImage(crisp))AimRenderer.Draw(g,new Rectangle(0,0,128,128),settings,lime,Color.Black,false);
            int rows=0;
            for(int y=0;y<128;y++)for(int x=0;x<128;x++){
                var pixel=crisp.GetPixel(x,y);
                Assert(pixel.A==0||pixel.ToArgb()==lime.ToArgb(),$"Integer {strokeWidth}px cross has exact color and no blurred edge pixels");
                if(x==70&&pixel.A>0)rows++;
                int mirror=strokeWidth%2==0?127:128;
                if(mirror-x<128&&mirror-y<128)Assert(pixel.A==crisp.GetPixel(mirror-x,mirror-y).A,"Opposite arms have symmetric pixel bounds");
            }
            Assert(rows==strokeWidth,"Integer stroke occupies exactly the requested pixel rows");
            using(var g=Graphics.FromImage(crisp)){g.Clear(Color.Transparent);settings.Opacity=50;AimRenderer.Draw(g,new Rectangle(0,0,128,128),settings,lime,Color.Black,false);}
            Assert(crisp.GetPixel(64,64).A is >=126 and <=128,"Overlapping arms do not accumulate opacity at the center");
            settings.Opacity=100;
            foreach(var background in new[]{Color.White,Color.FromArgb(210,170,140)}){
                using(var g=Graphics.FromImage(crisp)){g.Clear(background);AimRenderer.Draw(g,new Rectangle(0,0,128,128),settings,lime,Color.Black,false);}
                Assert(crisp.GetPixel(70,64).ToArgb()==lime.ToArgb(),"Bright backgrounds cannot show through the crisp stroke");
            }
        }
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
