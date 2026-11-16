# Aster Cross-Platform — PLAN

**Status:** Phase 0 (Windows) selesai dan **Avalonia GO**. Phase 1 **FAIL/waived** (Windows L2 belum lengkap; waiver terbuka). Phase 2 sedang berjalan: 2A Windows hardening **NOT RUN**, 2B Linux L2 **PARTIAL**. GPUI diarsipkan sebagai benchmark performa, bukan jalur produk.  
**Branch aktif:** `phase-3-release` (PR #1, menunggu merge)  
**Baseline:** Aster v0.10.0, commit `fcc6cb5`, MIT  
**Tujuan:** Membangun Aster L2 lintas platform dengan Avalonia, memakai Rust hanya untuk bagian yang terbukti butuh.

Dokumen ini adalah ringkasan eksekusi. Detail kontrak fitur, arsitektur, metrik, dan keputusan ada di:

- `docs/specs/2026-11-14-aster-cross-platform-design.md`
- `docs/plans/2026-11-14-aster-phase-0.md`
- `docs/plans/2026-11-14-windows-phase0-validation.md`
- `docs/plans/2026-11-16-aster-phase-2.md`
- `docs/specs/2026-11-16-aster-phase-2-design.md`
- `docs/results/phase0-report.md`
- `docs/results/phase2-report.md`
- `docs/results/phase2-matrix.md`

> Catatan: semua file di `docs/` kecuali `docs/aster-preview.png` dan `docs/aster-settings.png` bersifat local-only dan di-ignore Git. `PLAN.md` ini berada di root repo dan tidak termasuk aturan tersebut.

---

## 1. Keputusan stack (2026-11-14)

1. **UI produk: Avalonia 12.x (C#/.NET 10).**
2. **GPUI tidak dilanjutkan ke L2.** Revisi pin tetap disimpan sebagai benchmark performa; tidak ada dua aplikasi lengkap yang dipelihara.
3. **Backend: C# untuk service platform.** Rust hanya dipakai untuk hotspot yang terukur, di balik C ABI sempit atau sidecar (lihat §7).
4. **Arsitektur window:** island adalah window tersendiri seukuran pill (opsi 3). Expand panel me-resize window itu dan tetap interaktif; layer efek/glow opsional dibuat terpisah dan click-through.

Alasan utama:

- Hanya Avalonia yang lolos gate HUD Windows: transparan, topmost, no-activate, dan **click-through PASS** setelah host 760x460 diganti window pill (commit `8082955`).
- Avalonia NativeAOT PASS: 124 ms startup, 114.4 MB working set, 18.6 MB single file.
- Media SMTC PASS (YouTube di Edge, playing dan paused), WPF Release build PASS.
- GPUI masih gagal click-through dengan pendekatan `HTTRANSPARENT`, aksi tray belum di-wire, backend Wayland rusak di pin, dan API-nya terikat revisi git Zed tanpa stabilitas.
- Rust bukan kendala; batasnya adalah kematangan GPUI sebagai framework desktop. Menulis UI Rust dari nol berarti membangun layout, teks, animasi, aksesibilitas, dan IME sendiri.
- Kode yang ada 9.595 baris C#/.NET; service platform bisa dipindah hampir 1:1, hanya view XAML yang perlu ditulis ulang.

Bukti lengkap: `docs/results/phase0-report.md`, `avalonia-hud.md`, `avalonia-aot.md`, `media-probe.md`, `phase0-measurements.md`.

---

## 2. Prinsip kerja

- **Satu jalur produk.** Kontrak L2 hanya diimplementasikan di Avalonia; GPUI tidak lagi menjadi kandidat sejajar.
- **Hard gate dulu, baru skor.** Fitur wajib tidak boleh diganti klaim atau stub.
- **Performance target bersifat soft.** Sedikit di atas target tidak mendiskualifikasi selama produk nyaman dipakai.
- **Tidak menyentuh WPF.** `src/DynamicIsland`, `DynamicIsland.sln`, dan `build.ps1` tetap utuh sebagai referensi dan regression baseline.
- **Docs local-only.** Spesifikasi, rencana, hasil, dan ADR tidak di-commit; dua screenshot `docs/aster-preview.png` dan `docs/aster-settings.png` adalah exception yang di-track.
- **Bukti, bukan asumsi.** Setiap PASS punya screenshot, log, CSV, atau perintah yang bisa diulang.
- **Rust hanya dengan alasan terukur.** Jangan port service ke Rust tanpa benchmark yang menunjukkan target soft tidak tercapai.

---

## 3. Ringkasan fase

| Phase | Durasi | Fokus | Exit gate |
|---|---:|---|---|
| **0 — Foundation & Windows proof** | selesai 2026-11-14 | Worktree, toolchain, WPF baseline, HUD proof, AOT, media, harness | Avalonia HUD + AOT + media + WPF baseline tercatat |
| **1 — Avalonia Windows L2** | 1,5 minggu | Kontrak L2 di C#/Avalonia | **FAIL/waived** — Windows L2 belum lengkap; waiver terbuka |
| **2A — Windows hardening** | 1 minggu | Media restart, soak/attribution, visual/DPI, Narrator, frame pacing | **NOT RUN** — butuh host Windows |
| **2B — Linux L2** | 1–2 minggu | Shared UI, X11/XWayland, MPRIS, system adapters, AOT, soak | **PARTIAL** — core PASS; window no-activate, a11y, 60-min soak terbuka |
| **2 — macOS** | deferred | Backend macOS | **NOT RUN/deferred**; entry criteria di spec |
| **3 — Release engineering** | 1 minggu | CI, packaging AOT, draft release, README, ADR, gap register | **RELEASE CANDIDATE** — rc dry run inspected; final draft pending; gate Phase 2 terbuka dicatat, bukan PASS |

Total perkiraan: **5–6 minggu**, tergantung ketersediaan macOS dan hasil gate Phase 1.

---

## 4. Phase 0 — hasil Windows

Dijalankan di Windows 11 Home Single Language build 26300 (x64), Intel Core
Ultra 5 125H, Intel Arc Graphics, primary 2560x1440 @125%, secondary 2880x1800
@200%.

### Status

| Area | Status |
|---|---|
| Worktree, branch, toolchain (.NET 10.0.404, Rust 1.99.1 MSVC, VS Build Tools) | PASS |
| WPF Release build + publish (`dist\Aster.exe`, 75.4 MB) | PASS |
| WPF startup | NOT MEASURABLE (tool window, `MainWindowHandle`=0) |
| WPF idle (10 menit) | 461.8 MB working set, 1.55% CPU |
| Avalonia build + tests | PASS (5/5) |
| Avalonia managed publish | PASS (224 file) |
| Avalonia NativeAOT | PASS (18.6 MB, 124 ms, 114.4 MB, 0.16% CPU) |
| Avalonia HUD: transparan, topmost, no-activate, click-through | PASS |
| Avalonia tray (ikon + menu + aksi) | PASS |
| Avalonia DPI 150% | PASS (450x57 di 1055,0, DPI 144; tanpa sign-out) |
| Avalonia multi-monitor | PASS (tetap di primary) |
| GPUI MSVC build + tests | PASS (8/8) |
| GPUI HUD: click-through | FAIL (masih `HTTRANSPARENT`) |
| GPUI tray actions | NOT IMPLEMENTED |
| GPUI Wayland `primary_display()` | FAIL di pin |
| Media SMTC C# dan Rust | PASS (YouTube; playing dan paused) |

### Verdict

- **Avalonia: GO.** Semua hard gate HUD Windows terpenuhi: transparan, topmost, no-activate, click-through, tray (ikon + Show/Hide + Settings + Quit), DPI 125%/150%, multi-monitor, plus AOT, media SMTC, dan WPF build.
- **GPUI: NO-GO.** Diarsipkan sebagai benchmark; tidak masuk Phase 1.
- **Cross-platform: CONDITIONAL.** macOS belum tersedia; jalur Linux/macOS Avalonia belum diuji.

### Bukti

`docs/results/phase0-report.md`, `avalonia-hud.md`, `gpui-hud.md`,
`avalonia-aot.md`, `wpf-baseline.md`, `media-probe.md`,
`phase0-measurements.md`, `*.csv`, `screenshots/`.

---

## 5. Phase 1 — Avalonia Windows L2

> **Status: FAIL/waived.** Phase 1 tidak PASS. Windows L2 belum lengkap dan waiver Phase 1 masih terbuka; Phase 2A adalah pekerjaan penutupnya. Jangan mengklaim Phase 1 PASS.

### Scope

Implementasi kontrak **L2** di C#/Avalonia:

- Window: attached notch, transparent, topmost, no-activate, click-through, DPI, multi-monitor, fullscreen hide, single instance.
- Compact: clock, battery, Wi-Fi connected state, compact now-playing.
- Expanded: date/header, media artwork/title/artist, play/pause/prev/next/seek, source, settings button, dock/close.
- Settings: glass, bouncy, minimal notch, hide in fullscreen, autostart; separate window; persistence.
- Tray: show/hide, start with OS, settings, quit.
- Media: Windows SMTC, artwork, playback controls, progress interpolation.
- Tests: unit, integration, visual, soak, accessibility.

### Arsitektur

```text
spike/avalonia/
├─ Aster.Avalonia/            # Views, controls, view models, bootstrap
├─ Aster.Core/                # State, settings, models, interfaces
├─ Aster.Platform.Windows/    # SMTC, Win32, registry, WMI, WinRT
├─ Aster.Platform.MacOS/      # MediaRemote, LaunchAgents, AVFoundation
└─ Aster.Platform.Linux/      # MPRIS, .desktop, V4L2
```

Reuse dari Aster: `SettingsService` (dengan path/schema L2), `NetworkService`, dan sebagian model. Service Windows-only dipindah ke balik interface.

### Window architecture (hasil Phase 0)

- **Island window**: top-level window seukuran pill (300x38 DIP saat collapsed). Ini satu-satunya window interaktif di area atas layar.
- **Expand**: window yang sama di-resize ke ukuran panel; tidak ada host transparan penuh layar.
- **Effect layer** (opsional, nanti): window terpisah untuk glow/shadow dengan `WS_EX_LAYERED | WS_EX_TRANSPARENT`; tidak pernah menerima input.
- **Tray + settings**: `TrayIcon` dan window settings terpisah, seperti spike.
- **Style**: `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`, `WM_MOUSEACTIVATE` → `MA_NOACTIVATE`, topmost.

### Urutan kerja

1. Shared contracts + settings + state.
2. Window host: island, resize/expand, DPI, multi-monitor.
3. Compact state + compact now-playing.
4. Expanded media + dock/close.
5. Settings window + tray + autostart.
6. Media controls + artwork.
7. Tests, measurements, WPF regression build.
8. Tray/DPI 150% manual gates ditutup di awal fase.

### Exit criteria

- Semua Windows L2 acceptance criteria PASS.
- `dotnet build DynamicIsland.sln -c Release` tetap PASS.
- Metrik Windows tercatat: RAM idle/expanded/media, CPU idle/animasi, startup, binary.
- Tidak ada regresi WPF.

---

## 6. Phase 2 — Avalonia cross-platform

Phase 2 hanya berisi Avalonia. GPUI tidak diimplementasikan dan tetap menjadi
arsip benchmark; tidak ada pekerjaan UI Rust di fase ini. Spesifikasi:
`docs/specs/2026-11-16-aster-phase-2-design.md`. Rencana:
`docs/plans/2026-11-16-aster-phase-2.md`.

### 2A — Windows hardening

1. SMTC session recovery, serialized WinRT calls, start-time normalization, seek offset.
2. Soak/attribution harness: 5-minute dry run, UIA/no-UIA/polling-disabled runs, final 60 minutes; 30–60 slope <=5 MB/hour, minutes 10–60 handle range <=10.
3. Visual/DPI at 100/125/150%, light/dark wallpapers, live re-scale.
4. Narrator/keyboard walkthrough and shutdown settings flush.
5. Frame pacing with PresentMon.

Status: **NOT RUN** in this session. The Phase 1 waiver remains open.

### 2B — Linux L2 (Fedora + KDE Wayland + XWayland)

1. Shared UI extraction: `Aster.UI`, `Aster.UI.Abstractions`, `Aster.Desktop.Windows`, `Aster.Desktop.Linux`.
2. X11 window host: chrome, placement, DOCK fallback, no-activate/click-through probe.
3. MPRIS media: discovery/selection, metadata/artwork, play/pause/next/previous/seek, fake D-Bus player.
4. System adapters: sysfs/UPower battery, `.desktop` autostart, X11 fullscreen watcher.
5. Tray/settings/single instance, accessibility probe, native Wayland smoke.
6. NativeAOT publish + functional probes, metrics, 60-minute soak.

Status: **PARTIAL**. See `docs/results/phase2-report.md` and `docs/results/phase2-matrix.md`.

### macOS (deferred)

macOS is **not implemented** in Phase 2. Entry criteria: hardware or a cloud Mac, MediaRemote/LaunchAgents/NSStatusItem spike, and separate L2 validation. No `Aster.Platform.MacOS` is claimed.

### Validation matrix

- Windows, Linux KDE XWayland, native Wayland smoke, GNOME/Ubuntu, pure X11, macOS.
- Statuses only `PASS`, `PARTIAL`, `NOT RUN`, `BLOCKED`, each with evidence.
- The Linux claim is Fedora 45 + KDE Plasma Wayland through XWayland. GNOME/Ubuntu and pure X11 are not run.

### Exit criteria

- Matrix lengkap dengan bukti; `PARTIAL`/`NOT RUN` tidak dihitung PASS.
- Windows 2A gate hijau dan Phase 1 waiver ditutup.
- Linux L2: window no-activate/click-through, media, settings/tray, AOT, and 60-minute soak PASS.
- Accessibility manual walkthrough PASS.
- macOS tetap deferred dengan entry criteria.

---

## 7. Kebijakan penggunaan Rust

Rust **bukan** backend default. Service platform tetap C# karena API Windows/WinRT sudah tersedia dan kode WPF bisa dipindah hampir 1:1.

Pakai Rust hanya jika:

- ada hotspot terukur (CPU/memori) yang membuat target soft tidak tercapai, atau
- ada algoritma lintas platform yang lebih aman/cepat di Rust (mis. audio DSP, analisis media), atau
- ada kebutuhan isolasi proses yang lebih baik.

Bentuk integrasi:

- **C ABI `cdylib`**: struct sederhana, string UTF-8, kode error; dipanggil via `DllImport`.
- **Sidecar process**: komunikasi named pipe/IPC; lebih mudah diisolasi, ada biaya latensi/manajemen proses.
- **Hindari** callback kompleks, objek lintas bahasa, atau UI di Rust.

Packaging: native library per RID, disalin saat `dotnet publish`, diuji dengan AOT/trimming. Build cross-platform menjadi dua lapis (Rust targets × .NET RIDs).

Aturan: **tidak ada port ke Rust tanpa benchmark yang menunjukkan alasannya.**

---

## 8. Target dan gate

### Hard gate (Avalonia Windows)

- Transparent, topmost, no-activate, click-through.
- Tray (ikon + aksi), settings, media SMTC + artwork + kontrol.
- Compact now-playing, battery/Wi-Fi, fullscreen hide, autostart, single instance.
- DPI 125%/150%, multi-monitor.
- Stability: soak 1 jam tanpa crash/leak.

### Soft target

| Metrik | Target | Masih diterima |
|---|---:|---:|
| RAM idle | ~150 MB | ≤180 MB |
| RAM expanded + media | ~220 MB | ≤260 MB |
| CPU idle | ~1% | ≤2% |
| CPU animasi | ~3% | ≤5% |
| Startup | ~1 detik | ≤1,5 detik |
| Binary | ~50 MB | ≤70 MB |

Target ini tidak mendiskualifikasi; nilainya memengaruhi prioritas optimasi.

---

## 9. Worktree dan artefak

```text
aster/                  # primary checkout, branch phase-0
aster-avalonia/         # worktree aktif: spike/avalonia
aster-gpui/             # arsip benchmark: spike/gpui (tidak dikembangkan)
```

- Docs (`specs/`, `plans/`, `results/`, `adr/`) local-only; dua screenshot Avalonia di-track. Hanya kode/script/workflow/packaging/root docs yang di-commit.
- Owner menyetujui `src/Aster.Avalonia/` sebagai lokasi kode Phase 1; commit Phase 1 boleh mencakup `src/Aster.Avalonia/` selain `spike/` dan `scripts/`.
- Commit hanya kode/script di `spike/`, `scripts/`, `src/Aster.Avalonia/`, `.github/workflows/`, `packaging/`, dan root files `global.json`, `.gitattributes`, `CHANGELOG.md`, `THIRD-PARTY-NOTICES.txt`, `README.md`, `PLAN.md`.
- Jangan commit `.gitignore` kecuali diminta.
- Jangan commit `src/DynamicIsland`, `DynamicIsland.sln`, atau root build files.
- Jangan jalankan perintah yang mengubah metadata worktree dari Windows; path `.git` worktree menunjuk path Linux.

---

## 10. Risiko utama

| Risiko | Dampak | Mitigasi |
|---|---|---|
| WPF startup tidak terukur sebagai tool window | WPF baseline | Medium | Butuh keputusan owner (sinyal ready atau harness khusus) |
| Tidak ada hardware macOS | Cross-platform belum terverifikasi | Cloud Mac atau verdict CONDITIONAL |
| Port XAML WPF manual | Estimasi Phase 1 meleset | Port shell dulu; view per view dengan screenshot |
| AOT + native Rust library | Packaging/trimming bisa gagal | Uji per RID; mulai dari satu hotspot kecil |
| Expand panel + animasi di DPI berbeda | Posisi/hit-test meleset | Uji 125%/150% setiap perubahan window |
| Aksesibilitas/IME belum terbukti | Kualitas produk | Uji Narrator/Accessibility Insights di Phase 1 |
| Scope creep ke camera/shelf/lyrics | L2 tidak selesai | Bekukan L2; fitur lain setelah Phase 2 |
| Docs tidak sengaja ter-commit | Melanggar aturan local-only | `docs/*` ignored; stage path spesifik; cek `git status` |
| GPUI diinginkan kembali | Waktu terbuang | Hanya sebagai benchmark; keputusan butuh bukti baru |

---

## 11. Checkpoint

1. **Phase 1 shell** — island + expand + settings + tray berjalan.
2. **Phase 1 services** — media, battery, network, autostart.
3. **Metrik Phase 1** — RAM/CPU/startup/binary tercatat.
4. **Phase 2 matrix** — macOS/Linux PASS/PARTIAL.
5. **Release** — packaging, soak, ADR final.

---

## 12. Langkah berikutnya

1. Merge PR #1 di `ZaidaanArd/Aster` setelah review. CI sudah hijau di fork pada commit yang sama.
2. Jalankan gate Windows yang masih NOT RUN: media restart, soak 60 menit + attribution, visual/DPI, Narrator, frame pacing; tutup waiver Phase 1.
3. Selesaikan gate Linux yang masih PARTIAL: click-through 125/150/200%, manual accessibility, X11 fullscreen, expanded-media metrics, dan soak 60 menit.
4. Setelah merge, push tag `v0.11.0`, inspect draft, smoke archive Linux dan Windows, lalu publish manual.
5. macOS tetap deferred sampai ada host dan spike MediaRemote/LaunchAgents.

**Fokus sekarang adalah menutup gate yang terbuka dengan bukti, bukan menambah fitur.** Gate Windows Avalonia sudah terbukti di Phase 0; Phase 1 belum PASS dan tidak boleh diklaim PASS.

---

## 13. Phase 3 — Release engineering

Status: **RELEASE CANDIDATE** (2026-11-16). rc dry run inspected di fork; rilis final menunggu merge PR #1 di upstream dan publish manual.

- Desain: `docs/specs/2026-11-16-aster-phase-3-design.md` (local-only).
- Rencana: `docs/plans/2026-11-16-aster-phase-3.md` (local-only).
- Artefak target: `Aster-0.11.0-win-x64.zip`, `Aster-0.11.0-linux-x64.tar.gz`.
- Pipeline: `.github/workflows/ci.yml` (Linux/Windows build+test, Linux AOT smoke) dan `.github/workflows/release.yml` (guard, package, draft release).
- Verifikasi: `docs/results/phase3-gap-register.md` (local-only).
- Laporan: `docs/results/phase3-report.md` (local-only).
- ADR: `docs/adr/0001-distribution-baseline.md` (local-only).
- Klaim: portable preview, bukan L2 PASS. Gate Phase 2A dan gate Linux yang terbuka tetap tercatat.
