# Simple Aim Overlay

A customizable crosshair for Windows 10/11 (64-bit). No account required.

**[Download the latest installer](https://github.com/vvoldi/SimpleAimOverlay/releases/latest)**

Run `Setup.exe` and approve the administrator prompt. The first installation asks you to trust this project's self-signed certificate; Windows may show an unknown-publisher warning. Exit the app from its tray menu before a manual update.

- Choose a standard crosshair, or draw your own in **Editor** using pencil, lines, rectangles, ellipses, fill, symmetry and undo/redo.
- **F8** toggles the crosshair. Holding **Right Mouse** temporarily hides it. Click either binding in Settings to record up to three keys, including mouse buttons.
- **Settings → Language** switches between English (default) and Ukrainian immediately and remembers your choice.
- **Settings** controls closing/minimizing to the tray and startup with Windows or a game. PUBG is the default game; choose another EXE with **Browse**. **Show only in the selected game** hides the crosshair on the desktop and in other apps, including after Alt+Tab. It also starts the overlay when that game launches, using a small background watcher at Windows sign-in. Turn the option off for unrestricted visibility and to stop the watcher.
- In **Editor**, double-click a drawing to edit it, use **Ctrl + wheel** to zoom, and **Import / Export** to share a drawing (.aim preserves color mode; PNG works with other editors). Closing an edited drawing asks whether to save.
- **Settings → Configuration** imports/exports settings with drawings. Updates keep your configuration and create a backup; importing a configuration keeps this PC's startup choices.
- Updates are checked once at launch, or through **Settings → Check for updates**. Installation requires confirmation and may ask for administrator approval.

Fullscreen compatibility depends on the game and graphics mode. The app does not use game hooks.

Download speed reflects the last four seconds. **Settings → Open update logs** opens local diagnostics; backups are in the backups subfolder.

Settings and custom drawings stay in `%LOCALAPPDATA%\SimpleAimOverlay`.

[Report a problem](https://github.com/vvoldi/SimpleAimOverlay/issues) · [MIT license](LICENSE)
