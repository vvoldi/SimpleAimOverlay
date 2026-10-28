using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows.Forms;

static class Checks {
const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
[STAThread] static void Main(string[] args) {
var assembly=Assembly.Load("SimpleAimOverlay");
var number=assembly.GetType("SimpleAimOverlay.DarkNumberBox")!;
using(var box=(Control)Activator.CreateInstance(number,[-2000,2000,0])!) {
    var editor=(TextBox)number.GetField("editor",Private)!.GetValue(box)!;
    int Value()=>(int)number.GetProperty("Value")!.GetValue(box)!;
    editor.Text="-";Assert(Value()==0,"Intermediate minus");
    editor.Text="-125";Assert(Value()==-125,"Typed negative offset applies immediately");
    editor.Text="42";Assert(Value()==42,"Typed positive offset applies immediately");
    editor.Text="9999";Assert(Value()==42,"Out of range editing does not change value");
    number.GetMethod("Commit",Private)!.Invoke(box,null);Assert(Value()==2000&&editor.Text=="2000","Clamp on commit");
    editor.Text="9999";number.GetMethod("Commit",Private)!.Invoke(box,null);Assert(editor.Text=="2000","Normalize even when clamped value unchanged");
    editor.Text="";number.GetMethod("Commit",Private)!.Invoke(box,null);Assert(editor.Text=="2000","Empty input restored on commit");
}
RunNewChecks(assembly);
var updater=assembly.GetType("SimpleAimOverlay.AppUpdater")!;
var parseVersion=updater.GetMethod("ParseVersion",BindingFlags.NonPublic|BindingFlags.Static)!;
Assert((Version)parseVersion.Invoke(null,["v0.1.10-self-signed"])! > (Version)parseVersion.Invoke(null,["v0.1.9-self-signed"])!,"Numeric version ordering");
var parseRelease=updater.GetMethod("ParseRelease",BindingFlags.NonPublic|BindingFlags.Static)!;
string json="""
{"draft":false,"prerelease":false,"tag_name":"v0.1.9-self-signed","assets":[{"name":"SimpleAimOverlay-0.1.9-Setup.exe","browser_download_url":"https://github.com/vvoldi/SimpleAimOverlay/releases/download/v0.1.9-self-signed/SimpleAimOverlay-0.1.9-Setup.exe","digest":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","size":123}]}
""";
Assert(parseRelease.Invoke(null,[json])!=null,"Installer metadata");
Assert(parseRelease.Invoke(null,[json.Replace("\"prerelease\":false","\"prerelease\":true")])==null,"Skip prereleases");
try{parseRelease.Invoke(null,[json.Replace("https://github.com/","https://example.com/")]);throw new Exception("Untrusted URL accepted");}catch(TargetInvocationException e)when(e.InnerException is InvalidDataException){}
var handler=new Handler(json);
using var service=(IDisposable)Activator.CreateInstance(updater,Private,null,[handler],null)!;
using var form=new Form(); SynchronizationContext.SetSynchronizationContext(null); string status="";
var check=updater.GetMethod("CheckAsync")!;
void Run(bool manual)=>((Task)check.Invoke(service,[form,manual,(Action<string>)(s=>status=s),(Action)(()=>throw new Exception("Unexpected installation"))])!).GetAwaiter().GetResult();
Run(false);Run(false);Assert(handler.Count==1,"Only one automatic request per launch");Run(true);Assert(handler.Count==2,"Manual request allowed after startup");Assert(status.Contains("up to date"),"Current version status");
if(args.Length>0){
var verify=updater.GetMethod("VerifyAsync",BindingFlags.NonPublic|BindingFlags.Static)!;
string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0])));
((Task)verify.Invoke(null,[args[0],hash,CancellationToken.None])!).GetAwaiter().GetResult();
try{((Task)verify.Invoke(null,[args[0],new string('0',64),CancellationToken.None])!).GetAwaiter().GetResult();throw new Exception("Wrong hash accepted");}catch(InvalidDataException){}
}
Console.WriteLine("PASS: typed offsets, bounds, version ordering, release filtering, startup/manual request counts, checksum and publisher verification.");
}
static void RunNewChecks(Assembly assembly) {
    var game = assembly.GetType("SimpleAimOverlay.GameLauncher")!;
    var match = game.GetMethod("MatchesTarget", BindingFlags.NonPublic|BindingFlags.Static)!;
    Assert((bool)match.Invoke(null,["TslGame.exe","TslGame",null])!, "PUBG process filename match");
    Assert(!(bool)match.Invoke(null,[@"C:\Games\TslGame.exe","TslGame",@"D:\Other\TslGame.exe"])!,"Do not launch for a different executable path");
    Assert((bool)match.Invoke(null,[@"C:\Games\TslGame.exe","tslgame",@"c:\games\TslGame.exe"])!,"Case-insensitive exact executable match");
    Assert(!(bool)match.Invoke(null,[@"C:\Games\TslGame.exe","TslGame",null])!,"Explicit path fails closed when process path is unavailable");
    Assert((bool)game.GetMethod("IsRunning",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[Environment.ProcessPath!])!,"Read-only process detection matches the running executable path");
    var comboType=assembly.GetType("SimpleAimOverlay.DarkComboBox")!;
    using(var combo=(ComboBox)Activator.CreateInstance(comboType,true)!) {
        combo.Items.AddRange(["A","B","C"]);combo.SelectedIndex=1;combo.CreateControl();
        var message=Message.Create(combo.Handle,0x20a,new IntPtr(-120 << 16),IntPtr.Zero);
        comboType.GetMethod("WndProc",Private)!.Invoke(combo,[message]);
        Assert(combo.SelectedIndex==1,"Closed dropdown ignores wheel");
    }
    var canvasType=assembly.GetType("SimpleAimOverlay.DrawingCanvas")!;
    using var bitmap=new System.Drawing.Bitmap(32,32);
    using var canvas=(Control)Activator.CreateInstance(canvasType,[bitmap])!;
    canvasType.GetProperty("PenColor")!.SetValue(canvas,System.Drawing.Color.White);
    void Mouse(string method,int x,int y,MouseButtons button=MouseButtons.Left)=>canvasType.GetMethod(method,Private)!.Invoke(canvas,[new MouseEventArgs(button,1,x*10+1,y*10+1,0)]);
    Mouse("OnMouseDown",1,1);Mouse("OnMouseMove",20,1);Mouse("OnMouseUp",20,1);
    for(int x=1;x<=20;x++)Assert(bitmap.GetPixel(x,1).A==255,"Continuous fast pencil stroke");
    canvasType.GetProperty("MirrorX")!.SetValue(canvas,true);
    Mouse("OnMouseDown",3,5);Mouse("OnMouseUp",3,5);Assert(bitmap.GetPixel(28,5).A==255,"Mirrored stroke");
    canvasType.GetProperty("MirrorX")!.SetValue(canvas,false);
    canvasType.GetProperty("Tool")!.SetValue(canvas,"Rectangle");
    Mouse("OnMouseDown",5,10);Mouse("OnMouseMove",10,15);Mouse("OnMouseUp",10,15);
    Assert(bitmap.GetPixel(5,10).A==255&&bitmap.GetPixel(10,15).A==255&&bitmap.GetPixel(7,12).A==0,"Rectangle outline");
    canvasType.GetProperty("Tool")!.SetValue(canvas,"Fill");canvasType.GetProperty("PenColor")!.SetValue(canvas,System.Drawing.Color.Red);
    Mouse("OnMouseDown",7,12);Assert(bitmap.GetPixel(7,12).R==255&&bitmap.GetPixel(7,12).A==255&&bitmap.GetPixel(4,12).A==0,"Bounded flood fill");
    Mouse("OnMouseDown",7,12,MouseButtons.Right);Mouse("OnMouseUp",7,12,MouseButtons.Right);Assert(bitmap.GetPixel(7,12).A==0,"Right mouse erases");
    canvasType.GetProperty("Tool")!.SetValue(canvas,"Ellipse");
    Mouse("OnMouseDown",16,16);Mouse("OnMouseMove",26,26);Mouse("OnMouseUp",26,26);
    Assert(bitmap.GetPixel(26,21).A==255&&bitmap.GetPixel(21,16).A==255&&bitmap.GetPixel(21,21).A==0,"Ellipse outline");
    System.Drawing.Color picked=System.Drawing.Color.Empty;
    canvasType.GetEvent("ColorPicked")!.AddEventHandler(canvas,(Action<System.Drawing.Color>)(c=>picked=c));
    canvasType.GetProperty("Tool")!.SetValue(canvas,"Eyedropper");Mouse("OnMouseDown",26,21);
    Assert(picked.ToArgb()==System.Drawing.Color.Red.ToArgb(),"Eyedropper samples exact RGBA");
    var editorType=assembly.GetType("SimpleAimOverlay.PixelCrosshairEditor")!;
    using var editor=(Form)Activator.CreateInstance(editorType,[null,System.Drawing.Color.Cyan])!;
    var drawing=(Control)editorType.GetField("canvas",Private)!.GetValue(editor)!;
    var original=(System.Drawing.Bitmap)canvasType.GetProperty("Image")!.GetValue(drawing)!;
    original.SetPixel(32,32,System.Drawing.Color.Cyan);
    editorType.GetMethod("ResizeCanvas",Private)!.Invoke(editor,[128]);
    var resized=(System.Drawing.Bitmap)canvasType.GetProperty("Image")!.GetValue(drawing)!;
    Assert(resized.GetPixel(64,64).ToArgb()==System.Drawing.Color.Cyan.ToArgb(),"Resize preserves centered drawing");
    editorType.GetMethod("Undo",Private)!.Invoke(editor,null);
    var restored=(System.Drawing.Bitmap)canvasType.GetProperty("Image")!.GetValue(drawing)!;
    Assert(restored.Width==64&&restored.GetPixel(32,32).ToArgb()==System.Drawing.Color.Cyan.ToArgb(),"Undo restores canvas size and content");
    if(Environment.GetEnvironmentVariable("SAO_RENDER_EDITOR")=="1") {
        editor.Show(); Application.DoEvents();
        using var screenshot=new System.Drawing.Bitmap(editor.Width,editor.Height);editor.DrawToBitmap(screenshot,new System.Drawing.Rectangle(0,0,editor.Width,editor.Height));screenshot.Save(Path.Combine(Path.GetTempPath(),"SimpleAimOverlay-editor-0111.png"));
        editor.Close();
    }
    Console.WriteLine("PASS: game matching, closed dropdown wheel, pencil continuity, symmetry, rectangle, fill, eraser, resize and undo.");
}
sealed class Handler(string json):HttpMessageHandler{
public int Count;
protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Count++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)});}
}
}
