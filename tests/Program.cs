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
sealed class Handler(string json):HttpMessageHandler{
public int Count;
protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Count++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)});}
}
}
