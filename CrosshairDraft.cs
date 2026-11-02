using System.Text.Json;
namespace SimpleAimOverlay;

internal sealed class CrosshairDraft
{
    private AimSettings saved;
    internal CrosshairDraft(AimSettings settings) => saved = Capture(settings);
    private static AimSettings Capture(AimSettings source) { var result = new AimSettings(); CopyAppearance(source, result); return result; }
    internal bool HasChanges(AimSettings current) => JsonSerializer.Serialize(saved) != JsonSerializer.Serialize(Capture(current));
    internal void Commit(AimSettings current) => saved = Capture(current);
    internal void Restore(AimSettings current) => CopyAppearance(saved, current);
    private static void CopyAppearance(AimSettings from, AimSettings to)
    {
        to.Shape = from.Shape; to.Color = from.Color; to.OutlineColor = from.OutlineColor;
        to.Outline = from.Outline; to.OutlineWidth = from.OutlineWidth; to.OutlineOpacity = from.OutlineOpacity;
        to.Size = from.Size; to.ImageScale = from.ImageScale; to.Thickness = from.Thickness;
        to.Gap = from.Gap; to.Opacity = from.Opacity; to.OffsetX = from.OffsetX; to.OffsetY = from.OffsetY;
        to.Monitor = from.Monitor; to.ImagePath = from.ImagePath; to.TintImage = from.TintImage;
    }
}
