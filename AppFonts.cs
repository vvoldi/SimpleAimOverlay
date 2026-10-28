using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
namespace SimpleAimOverlay;
internal static class AppFonts
{
    private static PrivateFontCollection? collection;
    private static FontFamily family = FontFamily.GenericSansSerif;
    private static GCHandle pinnedFont;
    [DllImport("gdi32.dll")] private static extern IntPtr AddFontMemResourceEx(IntPtr font, uint size, IntPtr reserved, ref uint count);

    public static void Initialize()
    {
        if (collection != null) return;
        try
        {
            using var stream = typeof(AppFonts).Assembly.GetManifestResourceStream("SimpleAimOverlay.Assets.Fonts.Inter-Variable.ttf");
            if (stream == null) return;
            var bytes = new byte[stream.Length]; stream.ReadExactly(bytes);
            pinnedFont = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            collection = new PrivateFontCollection();
            uint count = 0; AddFontMemResourceEx(pinnedFont.AddrOfPinnedObject(), (uint)bytes.Length, IntPtr.Zero, ref count);
            collection.AddMemoryFont(pinnedFont.AddrOfPinnedObject(), bytes.Length);
            if (collection.Families.Length > 0) family = collection.Families[0];
        }
        catch { }
    }

    public static Font Regular(float size) => new(family, size, FontStyle.Regular, GraphicsUnit.Point);
    public static Font Semibold(float size) => new(family, size, FontStyle.Bold, GraphicsUnit.Point);
}


