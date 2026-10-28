## 0.1.11

- Remove bundled image presets and replace Gallery with Editor for personal drawings. Existing personal drawings are preserved; old bundled selections fall back to Cross.
- Add continuous pencil strokes, line/rectangle/ellipse tools, fill, eyedropper, X/Y symmetry, a live preview, dark canvas scrollbars, and Ctrl+Z/Ctrl+Y. Resizing keeps artwork centered instead of clearing it.
- Preserve custom drawing geometry instead of applying the old image thickness/gap transformations.
- Add an independent minimize-to-tray preference for the title bar minimize button.
- Suspend hold-to-hide and overlay z-order maintenance while the tray menu is open.
- Ignore mouse-wheel changes on closed dropdowns.
- Add opt-in game startup with PUBG selected by default. A background watcher starts at Windows sign-in, detects the selected EXE, and starts the overlay once per game session. Exact paths are matched when configured.
- Reduce README to installation and everyday usage instructions.
## 0.1.10

- Apply typed center offsets immediately; accept negative coordinates and normalize invalid/out-of-range edits on Enter or focus loss.
- Check the public GitHub release once per application launch, or manually through Settings > Check for updates. No periodic background polling.
- Offer verified installer downloads with progress. Check SHA-256 and the expected Authenticode publisher before requesting administrator approval.
- Wait for the old process to exit, replace the application atomically, preserve settings, and restart after an in-app update. Cancelling administrator approval keeps the current app running.
- Users on older versions need to install 0.1.10 manually once to receive the updater.
## 0.1.9

- Replace the color dialog with a saturation/value canvas, hue strip, RGBA and HSV inputs, hex entry, and current/original previews.
- Prevent native Windows focus borders from appearing on navigation tabs.
- Scroll the gallery by one row per wheel notch, including over its scrollbar; accumulate fine wheel deltas.
# Simple Aim Overlay 0.1.8

The main repository now distributes the same self-signed UIAccess installer used for personal and friends installations.

Download SimpleAimOverlay-0.1.8-Setup.exe. Exit the running app from the tray before installing. On first installation, Setup asks for permission to trust the bundled public test certificate and install the app in Program Files. This is not a publicly trusted publisher signature; Windows warnings may still appear.

Updates reuse the existing signing certificate and preserve settings. Existing friend-test installations use the same directory. Administrator approval can still be required for updates.

Includes the current dark interface, Inter font, image gallery, editor, and gallery scrolling fix.

SHA-256:
53ACCCBF41425D815900DB72EDF80B1303B37FBA8A9EE0125DC5A5C9C856C497
