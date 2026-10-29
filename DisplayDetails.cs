using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SimpleAimOverlay;

internal static class DisplayDetails
{
    internal static string Describe(Screen screen,int fallbackNumber)
    {
        string number=Regex.Match(screen.DeviceName,@"DISPLAY(\d+)$",RegexOptions.IgnoreCase).Groups[1].Value;
        if(number.Length==0)number=fallbackNumber.ToString();
        string? model=null;uint frequency=0;
        int width=screen.Bounds.Width,height=screen.Bounds.Height;
        try {
            var device=new DisplayDevice{Size=Marshal.SizeOf<DisplayDevice>()};
            if(EnumDisplayDevices(screen.DeviceName,0,ref device,1)) {
                // The interface path identifies this exact monitor, not just its model.
                string[] parts=device.DeviceId.Split('#');
                if(parts.Length>=3) {
                    using var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY\"+parts[1]+@"\"+parts[2]+@"\Device Parameters");
                    if(key?.GetValue("EDID") is byte[] edid)model=ReadEdidName(edid);
                }
                if(string.IsNullOrWhiteSpace(model)&&!string.IsNullOrWhiteSpace(device.Description)&&!device.Description.StartsWith("Generic",StringComparison.OrdinalIgnoreCase))model=device.Description;
            }
        }catch { /* Remote/virtual displays may not expose a hardware name. */ }
        try {
            var mode=new DeviceMode{Size=(ushort)Marshal.SizeOf<DeviceMode>()};
            if(EnumDisplaySettings(screen.DeviceName,-1,ref mode)) {
                if(mode.Width>0&&mode.Height>0){width=(int)mode.Width;height=(int)mode.Height;}
                if(mode.Frequency>1)frequency=mode.Frequency;
            }
        }catch { }
        return $"Display {number}"+(string.IsNullOrWhiteSpace(model)?"":" — "+model)+$" · {width} × {height}"+(frequency>1?$" · {frequency} Hz":"")+(screen.Primary?" · Primary":"");
    }
    internal static string? ReadEdidName(byte[] edid)
    {
        if(edid.Length<128||!edid.AsSpan(0,8).SequenceEqual(new byte[]{0,255,255,255,255,255,255,0}))return null;
        for(int offset=54;offset<=108;offset+=18) {
            if(edid[offset]!=0||edid[offset+1]!=0||edid[offset+2]!=0||edid[offset+3]!=0xfc)continue;
            string name=Encoding.ASCII.GetString(edid,offset+5,13).Split('\n','\0')[0].Trim();
            if(name.Length>0&&name.All(c=>c>=' '&&c<='~'))return name;
        }
        return null;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct DisplayDevice {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceKey;
    }
    // Native DEVMODEW layout; unused union fields stay zero.
    [StructLayout(LayoutKind.Explicit,Size=220)]
    private struct DeviceMode {
        [FieldOffset(68)] public ushort Size;
        [FieldOffset(172)] public uint Width;
        [FieldOffset(176)] public uint Height;
        [FieldOffset(184)] public uint Frequency;
    }
    [DllImport("user32.dll",EntryPoint="EnumDisplayDevicesW",CharSet=CharSet.Unicode)]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplayDevices(string device,uint index,ref DisplayDevice details,uint flags);
    [DllImport("user32.dll",EntryPoint="EnumDisplaySettingsW",CharSet=CharSet.Unicode)]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplaySettings(string device,int mode,ref DeviceMode details);
}
