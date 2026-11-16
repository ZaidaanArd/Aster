# Changelog

All notable changes to Aster are documented here.

## [0.11.0] - 2026-11-16

### Added

- Avalonia L2 portable preview for Windows x64 and Linux x64.
- NativeAOT archives with the required Skia, HarfBuzz, and ANGLE native libraries.
- GitHub Actions build and test pipeline for Linux and Windows.
- Tag-triggered draft release pipeline with SHA256 checksums.
- Single-instance, tray, settings, autostart, fullscreen hide, and platform media.

### Changed

- Product documentation now describes the Avalonia build; the WPF app is a legacy baseline.
- Version and SDK are pinned (`0.11.0`, .NET SDK `10.0.404`).

### Verification status

| Area | Status | Note |
|---|---|---|
| Windows 2A manual gates (G-01..G-05: media, soak, DPI, Narrator, frame pacing) | NOT RUN | No Windows host in the release session. |
| Phase 1 waiver (G-06) | BLOCKED | Depends on the Windows 2A gates. |
| Linux window click-through 125/150/200% (G-07) | PARTIAL | Real-pointer hand-off not proven at fractional/200%. |
| Linux 60-minute soak + expanded metrics (G-08) | NOT RUN | Five-minute dry run only. |
| Linux Orca/AT-SPI (G-09) | NOT RUN | Manual walkthrough pending. |
| X11 fullscreen hide (G-10) | PARTIAL | No real X11 fullscreen client walkthrough. |
| GNOME/Ubuntu and pure X11 (G-11) | NOT RUN | Matrix not run. |
| macOS (G-12) | NOT RUN | Deferred; no artifact. |
| Settings migration WPF -> Avalonia (G-13) | NOT RUN | Legacy `DynamicIsland/settings.json` is not imported. |
| AOT toolchain standard install (G-16) | PARTIAL | Phase 2 used a local RPM extraction; distro install pending. |
| WPF regression (G-17) | NOT RUN | Windows build not run. |

### Known limitations

- The Avalonia release does not include camera, shelf, focus timer, water reminder, lyrics, volume/brightness overlays, or mascot; those remain WPF-only.
- The Linux archive is built on Ubuntu 24.04 and requires glibc 2.39+.
- The Linux tray requires a StatusNotifierItem host; GNOME needs an extension.
- The binaries are unsigned; Windows SmartScreen warns.
- Keep the native libraries beside the executable.
- No installer, auto-update, code signing, or macOS artifact.
