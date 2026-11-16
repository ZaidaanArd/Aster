Aster {{VERSION}} - portable preview
====================================

Aster is a compact, always-on-top media island built with Avalonia and
published with NativeAOT. This is a portable preview, not the legacy WPF
application.

Keep the native libraries in the same folder as the executable. Moving only
the executable will make the app fail to start.

Windows
-------

1. Extract the zip.
2. Run Aster.exe.
3. Keep av_libglesv2.dll, libSkiaSharp.dll, and libHarfBuzzSharp.dll next to
   Aster.exe.
4. SmartScreen will warn because the binary is not signed. Choose
   "More info" -> "Run anyway".
5. Settings live in %LOCALAPPDATA%\AsterLab\Avalonia.

Requires Windows 10 version 2004 (build 19041) or later.

Linux
-----

1. Extract the tarball.
2. Run ./Aster.
3. Keep libSkiaSharp.so and libHarfBuzzSharp.so next to Aster.
4. Settings live in ~/.local/share/AsterLab/Avalonia.

Requires an X11 or XWayland session (KDE Plasma is the validated desktop),
glibc 2.39+, libicu, and the X11 libraries. The tray uses
StatusNotifierItem; KDE provides it. GNOME needs an extension. MPRIS controls
require an MPRIS-capable player.

Autostart records the current executable path. If you move the folder, turn
autostart off and on again.

Known limitations
-----------------

- No camera, shelf, focus timer, water reminder, lyrics, volume/brightness
  overlay, or mascot. Those are WPF-only.
- Unsigned binaries; Windows SmartScreen warns.
- No installer, auto-update, code signing, or macOS artifact.
- The Linux build requires a modern glibc.

Project: https://github.com/ZaidaanArd/Aster
Releases: https://github.com/ZaidaanArd/Aster/releases
