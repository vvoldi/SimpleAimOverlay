using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace SimpleAimOverlay;

internal sealed record FeedbackReport(string Kind,string Description,Dictionary<string,string> Diagnostics,string Language)
{
    internal static FeedbackReport Create(string kind,string description,StoreData data,int dpi,bool includeDiagnostics)
    {
        description=description.Trim();
        if(description.Length<10||description.Length>2000)throw new ArgumentException("Please write between 10 and 2000 characters.");
        if(kind is not ("bug" or "suggestion"))throw new ArgumentException("Invalid feedback type.");
        var d=new Dictionary<string,string>();
        if(includeDiagnostics){
            var aim=data.Current;
            d["App version"]=AppUpdater.CurrentVersion.ToString(3);
            d["Windows"]=RuntimeInformation.OSDescription;
            d["Architecture"]=RuntimeInformation.ProcessArchitecture.ToString();
            d["Runtime"]=RuntimeInformation.FrameworkDescription;
            d["UI scale"]=$"{dpi*100/96}%";
            d["Language"]=data.Language=="uk"?"uk":"en";d["Theme"]=data.Theme=="light"?"light":"dark";
            d["Displays"]=string.Join("; ",Screen.AllScreens.Select(s=>$"{s.Bounds.Width}x{s.Bounds.Height}{(s.Primary?" primary":"")}{(s.DeviceName==aim.Monitor?" selected":"")}"));
            d["Crosshair"]=new[]{"Cross","Cross + Dot","Dot","Circle","Image"}.Contains(aim.Shape)?aim.Shape:"Other";
            d["Geometry"]=FormattableString.Invariant($"size={aim.Size}; thickness={aim.Thickness}; gap={aim.Gap}; image zoom={aim.ImageScale}%; offset={aim.OffsetX},{aim.OffsetY}");
            d["Appearance"]=FormattableString.Invariant($"opacity={aim.Opacity}%; outline={aim.Outline}; outline width={aim.OutlineWidth}; outline opacity={aim.OutlineOpacity}%");
            d["Behavior"]=$"visible={aim.Visible}; hold to hide={aim.HideWhileHeld}; game only={data.LaunchWithGame}; close to tray={data.MinimizeToTrayOnClose}; minimize to tray={data.MinimizeToTray}";
        }
        return new(kind,description,d,data.Language=="uk"?"uk":"en");
    }
    internal string Preview()=>Description+"\r\n\r\n"+string.Join("\r\n",Diagnostics.Select(p=>$"{p.Key}: {p.Value}"));
    internal string Url(string endpoint)
    {
        if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo))throw new InvalidOperationException("Feedback service is not configured yet.");
        var json=JsonSerializer.Serialize(this,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        return endpoint.TrimEnd('/')+"/#"+Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+','-').Replace('/','_');
    }
}
