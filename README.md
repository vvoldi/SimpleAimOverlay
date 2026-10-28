# Simple Aim Overlay

A lightweight, single-file Windows crosshair overlay. Configure its shape, color, outline, opacity, size, thickness, gap, monitor, and position; browse and recolor 20 bundled crosshairs; draw and edit pixel-art crosshairs; import custom images; keep multiple profiles; and toggle visibility with a hotkey or while holding a mouse button or key. Image crosshairs support size, thickness, gap, outline, tint, and opacity adjustments. Closing the window can minimize the app to the system tray.

The interface uses a dark custom control set, English labels, the bundled Inter typeface, and a matching crosshair icon across the app window, taskbar, and tray. Inter is licensed under the SIL Open Font License 1.1; the license is embedded in the app and available at `Assets/Fonts/OFL.txt`.

The overlay is an independent, click-through desktop window. The public edition does not inject code into games or modify game processes. Visibility in fullscreen depends on Windows, the graphics driver, the game, and its fullscreen presentation mode.

## Requirements

- Windows x64
- .NET 10 SDK to build from source

## Build

Build a normal single-file executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

The output is `bin\Release\net10.0-windows\win-x64\publish\SimpleAimOverlay.exe`.

To build the UIAccess variant, add `-p:UseUiAccess=true`. Windows grants UIAccess only to an Authenticode-signed executable whose signature chains to a trusted certificate and which runs from a protected location such as `Program Files`. A local build by itself will not meet those requirements. See [Microsoft's UIAccess requirements](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-securityoverview).

The GitHub Actions workflow builds the UIAccess single-file executable as an unsigned artifact for the project's approved signing workflow. It is not ready for public installation until a trusted signing provider signs it.

## Data and privacy

Settings and imported crosshair images are stored locally under `%LOCALAPPDATA%\SimpleAimOverlay`. The app does not require an account or network connection.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).

## Code signing policy

See [CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md). SignPath Foundation approval is pending; no public signed release is available yet.
