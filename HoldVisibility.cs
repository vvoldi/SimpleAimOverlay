namespace SimpleAimOverlay;

internal static class HoldVisibility
{
    // Evaluate held state, not press order. The override only cancels hold-to-hide;
    // the master visibility toggle and selected-game filter still apply separately.
    internal static bool IsHidden(AimSettings settings, bool hideHeld, bool overrideHeld) =>
        settings.HideWhileHeld && hideHeld && !(settings.OverrideHideWhileHeld && overrideHeld);
}
