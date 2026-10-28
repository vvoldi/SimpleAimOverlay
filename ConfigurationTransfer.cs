using System.Text.Json;
using System.Drawing.Imaging;

namespace SimpleAimOverlay;

internal static class ConfigurationTransfer
{
    internal sealed record Drawing(string Name, bool Tint, string Png);
    internal sealed record Package(int Format, StoreData Settings, Drawing[] Drawings);
    internal static Drawing ReadDrawing(string file, bool tint) => new(Path.GetFileNameWithoutExtension(file),tint,Convert.ToBase64String(File.ReadAllBytes(file)));
    internal static Bitmap Decode(Drawing drawing) {
        byte[] bytes=Convert.FromBase64String(drawing.Png);
        if(bytes.Length>8_000_000)throw new InvalidDataException("Drawing is too large.");
        using var stream=new MemoryStream(bytes);using var source=Image.FromStream(stream);
        if(source.Width>1024||source.Height>1024)throw new InvalidDataException("Drawings must be at most 1024 × 1024 pixels.");
        return ((Bitmap)source).Clone(new Rectangle(0,0,source.Width,source.Height),PixelFormat.Format32bppArgb);
    }
    internal static string InstallDrawing(Drawing drawing) {
        using var image=Decode(drawing);
        string folder=Path.Combine(Store.Root,"crosshairs");Directory.CreateDirectory(folder);
        string safe=string.Concat(drawing.Name.Take(80).Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));
        string name=$"{safe}-{Guid.NewGuid():N}.png";image.Save(Path.Combine(folder,name),ImageFormat.Png);return name;
    }
    internal static void Export(string path, StoreData data) {
        var drawings=data.Images.Where(n=>Path.GetFileName(n)==n&&File.Exists(Path.Combine(Store.Root,"crosshairs",n))).Select(n=>ReadDrawing(Path.Combine(Store.Root,"crosshairs",n),!data.OriginalColorImages.Contains(n))).ToArray();
        // Use filenames in the portable copy, not paths from this computer.
        var copy=JsonSerializer.Deserialize<StoreData>(JsonSerializer.Serialize(data))!;
        copy.Current.ImagePath=Path.GetFileName(copy.Current.ImagePath);
        File.WriteAllText(path,JsonSerializer.Serialize(new Package(1,copy,drawings)));
    }
    internal static StoreData Import(string path) {
        if(new FileInfo(path).Length>64_000_000)throw new InvalidDataException("Configuration is too large.");
        var package=JsonSerializer.Deserialize<Package>(File.ReadAllText(path))??throw new InvalidDataException("Invalid configuration.");
        if(package.Format!=1||package.Settings?.Current==null||package.Drawings==null||package.Drawings.Length>256)throw new InvalidDataException("Unsupported configuration format.");
        foreach(var drawing in package.Drawings){using var check=Decode(drawing);}
        var data=package.Settings;
        if(!float.IsFinite(data.Current.Thickness)||!float.IsFinite(data.Current.OutlineWidth))throw new InvalidDataException("Invalid thickness.");
        foreach(string key in new[]{data.Current.Hotkey,data.Current.HoldKey})if(KeyBindingButton.Parse(key).Length is <1 or >3||KeyBindingButton.Parse(key).Contains(Keys.None))throw new InvalidDataException("Invalid key binding.");
        string selected=Path.GetFileNameWithoutExtension(data.Current.ImagePath);
        data.Images=[];data.OriginalColorImages=[];data.Current.ImagePath="";
        foreach(var drawing in package.Drawings){string name=InstallDrawing(drawing);data.Images.Add(name);if(!drawing.Tint)data.OriginalColorImages.Add(name);if(drawing.Name==selected)data.Current.ImagePath=Path.Combine(Store.Root,"crosshairs",name);}
        if(data.Current.Shape=="Image"&&!File.Exists(data.Current.ImagePath))data.Current.Shape="Cross";
        return data;
    }
    internal static string Backup() {
        string folder=Path.Combine(Store.Root,"backups");Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,$"settings-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.aimconfig");Export(path,Store.Load());
        foreach(var old in new DirectoryInfo(folder).GetFiles("*.aimconfig").OrderByDescending(f=>f.LastWriteTimeUtc).Skip(5))old.Delete();return path;
    }
}
