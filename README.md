# Dynamic Island for Windows

Notch kecil di tengah atas layar Windows, terinspirasi dari [Nusa](https://rama2555.github.io/nusa/).
Dibuat dari nol dengan **C# / WPF (.NET 8)** — bukan salinan kode Nusa.

![Windows 10/11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)

## Fitur

- **Notch selalu di atas** — hitam, membulat, menempel di tepi atas, tidak muncul di taskbar/Alt+Tab.
- **Hover untuk membuka** — animasi spring halus, konten muncul ke bawah.
- **Jam + tanggal** — jam di pill, tanggal & status di panel.
- **Tab Music / Camera / Focus** — panel media, kamera, dan focus timer; ukuran panel tetap 500×252.
- **Transisi panel** — slide + fade halus saat pindah tab, highlight tab ikut bergeser.
- **Lyrics di dalam Music** — tombol speech bubble membuka panel lirik (204px) di kanan; media
  (260px) dan progress bergeser ke kiri dengan animasi morph/slide dari kanan, tanpa fade,
  tanpa tab terpisah dan tanpa kontrol start/pause ganda.
  Synced lyrics dari LRCLIB dengan baris aktif ber-accent biru, fade atas/bawah, distance dimming,
  auto-scroll halus, klik baris untuk seek, fallback plain lyrics, dan cache lokal.
- **Camera** — tab kamera dengan preview live 16:9, mirror toggle, dan shutter. Foto
  full-resolution disimpan ke `Pictures\DynamicIsland\Camera`. Kamera hanya aktif selama
  tab Camera terbuka dan island expanded.
- **Focus timer (Pomodoro)** — 25/5/15 (bisa diubah), start/pause/reset, progress, session counter,
  chime, dan saran short/long break setiap 4 sesi. Saat timer jalan, pill menampilkan sisa waktu.
- **Water reminder** — toast droplet di pill sesuai interval, header counter `3/8`, klik untuk
  mencatat gelas. Reminder berhenti saat goal tercapai dan ditahan saat island disembunyikan.
- **Media player** — judul, artis, album art, progress, play/pause, previous, next, dan seek.
  Mendukung Spotify, Apple Music, YouTube/Chrome/Edge/Firefox via Windows media session.
- **Volume overlay** — muncul otomatis saat volume berubah.
- **Brightness overlay** — untuk layar laptop (WMI), update maksimal ~1,2 detik.
- **Baterai & Wi-Fi** — ikon status, toast saat charger dicolok.
- **Mascot** — jam memudar, mata muncul saat idle, berkedip, dan tertidur.
- **Minimal notch** — mode garis tipis.
- **Auto-hide saat fullscreen** — game/video fullscreen tidak terganggu (bisa dimatikan).
- **Settings window** — dark theme, bisa diakses dari tray atau ikon gear di panel.
- **Tray menu** — show/hide, settings, start with Windows, minimal notch, mascot, hide in fullscreen, about, exit.
- **Ringan saat idle** — sekitar 0,3–1,5% CPU (satu core) saat tidak ada animasi.

## Menjalankan

Versi siap pakai ada di `dist\DynamicIsland.exe` (self-contained, tidak perlu install .NET).
Versi ZIP: `dist\DynamicIsland-0.6.0-win-x64.zip`.
Double-click, lalu cari ikonnya di system tray (mungkin masuk ke overflow `^` — klik panah itu,
lalu drag ikonnya ke taskbar kalau mau selalu terlihat).

> Exe belum ditandatangani, jadi Windows SmartScreen bisa menampilkan peringatan.
> Klik **More info → Run anyway**.

## Pemakaian

| Aksi | Hasil |
|---|---|
| Arahkan kursor ke pill | Panel terbuka |
| Jauhkan kursor | Panel menutup (delay ~0,3 detik) |
| Klik ikon **Music** | Panel media player (dengan animasi geser) |
| Klik ikon **Camera** | Panel kamera; kamera mulai otomatis saat panel terbuka |
| Klik **shutter** di tab Camera | Simpan foto full-resolution ke Pictures |
| Toggle **Mirror** | Putar preview kamera kiri-kanan |
| Klik ikon **Focus** | Panel focus timer (dengan animasi geser) |
| Klik ikon **speech bubble** di Music | Buka/tutup panel lirik; media bergeser ke kiri |
| Klik baris lirik | Seek ke timestamp baris tersebut |
| Klik droplet `3/8` di header | Catat satu gelas air |
| Klik toast water reminder | Catat satu gelas air |
| Klik ikon **gear** | Buka settings |
| Klik progress bar | Seek ke posisi tersebut |
| Klik ikon tray | Buka menu pengaturan |
| Klik dua kali ikon tray | Sembunyikan/tampilkan island |

### Focus timer

- Pilih **Focus**, **Short break**, atau **Long break** lewat chip di panel.
- Tekan tombol play untuk mulai; tekan lagi untuk pause; tombol reset untuk kembali ke durasi penuh.
- Saat timer berjalan, pill collapsed menampilkan sisa waktu (amber). Saat selesai, muncul
  toast + chime dan tombol **Start break** (long break disarankan setiap 4 sesi).
- Durasi bisa diubah di **Settings → Focus** (1–180 menit).
- Timer bersifat **session-only**: kalau app ditutup, timer reset. Session counter juga reset saat app dibuka lagi.

### Camera

- Buka tab **Camera**; kamera aktif otomatis saat island expanded. Pindah tab, collapse,
  hide, atau fullscreen akan mematikan kamera.
- Preview **16:9** dengan mirror default. Toggle **Mirror** kalau tidak ingin dibalik.
- Tombol **shutter** menyimpan foto full-resolution ke
  `Pictures\DynamicIsland\Camera\camera-YYYYMMDD-HHmmss.jpg`.
  Foto disimpan **tidak mirror** (orientasi asli), walau preview dibalik.
- Kalau ada lebih dari satu kamera, tombol **Switch camera** muncul.
- Tidak memakai mikrofon, tidak merekam video, dan tidak upload apa pun.
- Kalau akses kamera diblokir atau kamera sedang dipakai app lain, muncul tombol
  **Retry** / **Camera settings**.

### Water reminder

- Aktifkan di **Settings → Water → Water reminder**.
- Atur **interval** (1–240 menit), **active hours** (0–23), dan **daily goal** (1–20 gelas).
  Active hours mendukung rentang lewat tengah malam, misalnya 22 → 08. Kalau start = end,
  reminder aktif 24 jam.
- Saat waktunya, toast droplet muncul di pill meskipun panel tertutup:
  `Time to drink · 3/8`. Klik toast untuk mencatat satu gelas.
- Header panel menampilkan `💧 3/8` saat fitur aktif; klik untuk mencatat gelas tanpa menunggu reminder.
  Angka berubah hijau saat goal tercapai.
- Counter disimpan di `settings.json` dan reset otomatis saat tanggal berganti.
- Reminder berhenti setelah goal harian tercapai, dan ditahan saat island disembunyikan
  (fullscreen atau hide dari tray).
- Tombol **Log a glass**, **Preview**, dan **Reset today** tersedia di Settings untuk testing.

### Lyrics

- Buka panel lirik lewat ikon **speech bubble** di tab Music. Media (260px) dan progress bar
  bergeser ke kiri; panel lirik (204px) slide masuk dari kanan — morph, tanpa fade. Kontrol
  start/pause tetap di kolom Music.
- Lirik diambil dari [LRCLIB](https://lrclib.net) berdasarkan judul, artis, album, dan durasi.
- Synced lyrics: baris aktif memakai accent bar biru + pill, baris lain diredupkan berdasarkan
  jarak, dan auto-scroll bergerak halus (220ms). Klik baris untuk seek.
- Header menampilkan status `SYNCED` / `PLAIN` / `INSTRUMENTAL`; refresh berputar saat loading.
- Fade halus di atas/bawah daftar supaya teks tidak terpotong kasar.
- Kalau tidak ada synced lyrics, ditampilkan plain lyrics. Kalau tidak ada sama sekali, muncul pesan.
- Tombol refresh untuk mencari ulang; hasil disimpan di cache lokal 30 hari.
- Lirik hanya di-fetch saat panel lirik dibuka. Kirim hanya metadata track — tidak ada data pribadi.
- Bisa dimatikan di **Settings → Lyrics → Fetch lyrics from LRCLIB**.
- Cache bisa dihapus di **Settings → Data → Clear lyrics cache**.

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
├─ Controls/IslandView.*      notch, animasi, jam, mascot, media UI, toast, tab bar
├─ Controls/FocusView.*       UI focus timer (chips, countdown, progress, kontrol)
├─ Controls/CameraView.*      tab kamera (preview, mirror, shutter, status)
├─ Controls/LyricsView.*      pane lirik (header, status, refresh, highlight, auto-scroll)
├─ SettingsWindow.*           settings window dark (general, appearance, lyrics, focus, water, data)
├─ Services/
│  ├─ MediaService.cs         WinRT GSMTC: now playing, artwork, kontrol
│  ├─ LyricsService.cs        LRCLIB: fetch, parse LRC, cache, cancellation
│  ├─ FocusService.cs         Pomodoro: phase, countdown, session counter, break suggestion
│  ├─ CameraService.cs        WinRT MediaCapture: preview frames, mirror, full-res photo
│  ├─ WaterService.cs         water reminder: interval, active hours, goal, daily count
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
└─ Themes/Theme.xaml          warna, font, style tombol, toggle, input, tab
```

## Keterbatasan

- **Bukan open-source Nusa.** Nusa memakai Rust dan tidak membuka source-nya; proyek ini implementasi independen.
- **Memori** sekitar 250–320 MB untuk build self-contained (WPF + WinRT + runtime .NET). Nusa yang native Rust jauh lebih kecil.
- **Brightness** hanya untuk layar internal laptop. Monitor eksternal butuh DDC/CI (belum diimplementasikan).
- **Metadata media** tergantung aplikasi. Spotify/YouTube/Apple Music berjalan baik; app DRM tertentu tidak mengekspos metadata.
- **Lyrics** tergantung ketersediaan di LRCLIB; beberapa track tidak punya lirik atau server bisa sedang sibuk. Tombol refresh tersedia.
- **Fullscreen exclusive** tidak bisa ditimpa; island akan disembunyikan.
- **Focus timer** belum persist kalau app ditutup atau PC sleep; timer dihitung wall-clock.
- **Water reminder** hanya muncul saat island terlihat; reminder yang jatuh tempo saat app tersembunyi
  akan ditampilkan begitu island muncul lagi. Counter disimpan per tanggal lokal.
- **Camera** membutuhkan izin kamera Windows dan bisa gagal kalau kamera sedang dipakai app lain.
  Preview di-throttle ~15fps (640×360); foto tetap full-resolution. Kamera hanya hidup saat tab
  Camera terbuka dan island expanded.
- **Snapshot** disimpan ke folder Pictures pengguna; kalau OneDrive mengalihkan Pictures, file
  ikut masuk ke folder OneDrive tersebut.
- **Installer belum ditandatangani**, sehingga SmartScreen memberi peringatan.

## Roadmap

Fitur Nusa yang belum ada di versi ini: to-do dengan reminder dan file shelf drag & drop.

## Lisensi

MIT — lihat [LICENSE](LICENSE).
