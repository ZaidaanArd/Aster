# Dynamic Island for Windows

Notch kecil di tengah atas layar Windows, terinspirasi dari [Nusa](https://rama2555.github.io/nusa/).
Dibuat dari nol dengan **C# / WPF (.NET 8)** — bukan salinan kode Nusa.

![Windows 10/11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)

## Fitur

- **Notch selalu di atas** — hitam, membulat, menempel di tepi atas, tidak muncul di taskbar/Alt+Tab.
- **Hover untuk membuka** — animasi spring halus, konten muncul ke bawah.
- **Jam + tanggal** — jam di pill, tanggal & status di panel.
- **Media player** — judul, artis, album art, progress, play/pause, previous, next, dan seek.
  Mendukung Spotify, Apple Music, YouTube/Chrome/Edge/Firefox via Windows media session.
- **Volume overlay** — muncul otomatis saat volume berubah.
- **Brightness overlay** — untuk layar laptop (WMI), update maksimal ~1,2 detik.
- **Baterai & Wi-Fi** — ikon status, toast saat charger dicolok.
- **Mascot** — jam memudar, mata muncul saat idle, berkedip, dan tertidur.
- **Minimal notch** — mode garis tipis.
- **Auto-hide saat fullscreen** — game/video fullscreen tidak terganggu (bisa dimatikan).
- **Tray menu** — show/hide, start with Windows, minimal notch, mascot, hide in fullscreen, about, exit.
- **Ringan saat idle** — sekitar 0,3–1,5% CPU (satu core) saat tidak ada animasi.

## Menjalankan

Versi siap pakai ada di `dist\DynamicIsland.exe` (self-contained, tidak perlu install .NET).
Double-click, lalu cari ikonnya di system tray (mungkin masuk ke overflow `^`).

> Exe belum ditandatangani, jadi Windows SmartScreen bisa menampilkan peringatan.
> Klik **More info → Run anyway**.

## Pemakaian

| Aksi | Hasil |
|---|---|
| Arahkan kursor ke pill | Panel terbuka |
| Jauhkan kursor | Panel menutup (delay ~0,3 detik) |
| Klik album art / progress bar | Seek ke posisi tersebut |
| Klik ikon tray | Buka menu pengaturan |
| Klik dua kali ikon tray | Sembunyikan/tampilkan island |

Pengaturan disimpan di:

```
%LOCALAPPDATA%\DynamicIsland\settings.json
```

## Build dari source

Prasyarat: Windows 10 1809+ / Windows 11, [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
# build debug
.\build.ps1

# build release self-contained (single exe)
.\build.ps1 -Publish
```

Atau manual:

```powershell
dotnet build src\DynamicIsland\DynamicIsland.csproj -c Debug
dotnet publish src\DynamicIsland\DynamicIsland.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -o dist
```

Jika SDK dipasang per-user (tanpa admin) di `%LOCALAPPDATA%\Microsoft\dotnet`,
set `DOTNET_ROOT` ke folder tersebut sebelum menjalankan build Debug.

## Struktur

```
src/DynamicIsland/
├─ App.xaml(.cs)              startup, single instance, service wiring
├─ MainWindow.xaml(.cs)       window topmost/transparent, posisi, hit-test, tray commands
├─ Controls/IslandView.*      notch, animasi, jam, mascot, media UI, toast
├─ Services/
│  ├─ MediaService.cs         WinRT GSMTC: now playing, artwork, kontrol
│  ├─ AudioService.cs         NAudio: volume + notifikasi
│  ├─ BrightnessService.cs    WMI brightness
│  ├─ BatteryService.cs       baterai/charging
│  ├─ NetworkService.cs       status Wi-Fi
│  ├─ FullscreenWatcher.cs    deteksi fullscreen
│  ├─ SettingsService.cs      settings JSON
│  ├─ StartupService.cs       autostart registry
│  ├─ TrayService.cs          NotifyIcon + menu
│  └─ DebugLog.cs             log debug (hanya build Debug)
├─ Interop/NativeMethods.cs   P/Invoke Win32
└─ Themes/Theme.xaml          warna, font, style tombol
```

## Keterbatasan

- **Bukan open-source Nusa.** Nusa memakai Rust dan tidak membuka source-nya; proyek ini implementasi independen.
- **Memori** sekitar 250–320 MB untuk build self-contained (WPF + WinRT + runtime .NET). Nusa yang native Rust jauh lebih kecil.
- **Brightness** hanya untuk layar internal laptop. Monitor eksternal butuh DDC/CI (belum diimplementasikan).
- **Metadata media** tergantung aplikasi. Spotify/YouTube/Apple Music berjalan baik; app DRM tertentu tidak mengekspos metadata.
- **Fullscreen exclusive** tidak bisa ditimpa; island akan disembunyikan.
- **Installer belum ditandatangani**, sehingga SmartScreen memberi peringatan.

## Roadmap

Fitur Nusa yang belum ada di versi ini: lyrics (LRCLIB), pomodoro, water reminder,
to-do dengan reminder, dan file shelf drag & drop.

## Lisensi

MIT — lihat [LICENSE](LICENSE).
