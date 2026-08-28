# Force-install Elgato Wave Link 3.x on Windows 10 — End-to-end Research / Reproduction Kit

> ⚠️ **Disclaimer**: This project is not affiliated with Elgato / Corsair. It is for technical research, compatibility verification, and local reproduction only.
> All Elgato proprietary assets (driver MSI, INF, MSIX package) are copyrighted by Elgato and must be used in accordance with their license.
> Not intended for commercial use or for circumventing normal product licensing. See [`NOTICE`](./NOTICE).
> 中文说明见 [README.md](./README.md). Principles and manual steps are in [FAQ_EN.md](./FAQ_EN.md).

---

## Abstract

Elgato Wave Link 3.x is officially Windows 11 only. This repository provides the **end-to-end** artifact set to get it running on **Windows 10** — from "the app won't install" to "the driver is ready":

1. **Step 0 (the app)**: lower the MSIX `MinVersion=22000` (Win11 gate) to Windows 10, repack, and install unsigned under Developer Mode.
2. **Step 1 (the driver)**: bypass the "server does not push the driver" block and force-install the official audio driver, then verify.

**No Elgato proprietary app binaries are shipped** (you supply the MSIX locally — see `input/`); only research artifacts, scripts, and the official public-CDN driver (~3 MB) are included.

## Background

Elgato Wave Link 3.x targets Windows 11 only (MSIX `MinVersion=22000`). On **Windows 10 22H2 (19045)** the app hangs at "Install driver". Root cause: at runtime Wave Link queries the server `device-update-check.php`, and for `osVersion=10.0.19045.0` the server returns an empty list (`appDevices:[]`), so the audio driver is never delivered.

Reverse-engineering and live testing confirm the block exists in only two layers (MSIX version gate + server-side driver delivery), while **the driver binaries themselves are open to Windows 10**. This repo ships the full artifact set and one-click scripts to bypass both.

## Compatibility Scope

Based on the OS-version decorations found in the 4 INF files embedded in the driver MSI (see `evidence/inf/`):

| INF | Minimum Windows 10 | Build |
|---|---|---|
| ElgatoUsbAudio.inf | 1809 | 17763 |
| ElgatoUsbAudioks.inf | 1809 | 17763 |
| ElgatoVirtUsbAudioEmu.inf | 1803 | 17134 |
| ElgatoUsbAudio_dfu.inf | no lower bound (all NT versions) | — |

Key findings:

- **Verified**: Windows 10 22H2 (19045).
- **Theoretically supported (driver layer)**: Windows 10 **1809 (17763) and every later build** (1809 / 1903 / 1909 / 2004 / 20H1 / 20H2 / 21H1 / 21H2 / 22H2). The main drivers (UsbAudio, UsbAudioks) require 1809+; the VirtUsbAudioEmu sub-driver can go as low as 1803.
- **No Win11 hard-lock**: none of the 4 INF decorations carry a 22000+ (Windows 11) exclusive gate — they are open to the entire Windows 10 line.
- **Not supported**: Windows 10 1709 (16299) and earlier (below 17134).
- **Prerequisite for app auto-install**: Step 0's "unsigned install" relies on the Developer-Mode `-AllowUnsigned` capability, available on **Windows 10 2004 (19041) and newer**. On **1809 / 1909** you must re-sign the repacked MSIX with a trusted certificate (.pfx) — see FAQ_EN.md "Manual re-sign route". **The driver MSI itself is supported on 1809+.**

> Note: 1809+ is **theoretical support** derived from the INF OS decorations; only 22H2 has been empirically tested. If you succeed on another Windows 10 build, please open an issue/PR to extend the verified matrix.

## Step-by-Step Guide

> This guide follows the **one-click script** as the main path: a single command performs *bypass the Win11 gate + install driver + verify*. For the underlying principle or manual methods, see [FAQ_EN.md](./FAQ_EN.md).

## GUI Installer (WaveLinkWin10Setup.exe)

If you prefer not to use the command line, this repo also ships a **native Windows GUI installer** (a single C#/.NET 8 exe) that wraps *place MSIX → patch → install app → install driver → verify* into one window with a live log. It is a feature-equivalent wrapper of the script (`scripts/setup_wavelink_win10.ps1`) — pick either.

### Get the exe
- **GitHub Releases**: download `WaveLinkWin10Setup.exe` from the repo's Releases page (self-contained single file, ~**150 MB**, no pre-installed .NET runtime needed): https://github.com/DRDRDRRDDRDR/wavelink-win10-driver/releases
- Or build it yourself (see "Build from source" below).

### Usage
1. Place your official MSIX into `input\` next to the exe.
2. Double-click `WaveLinkWin10Setup.exe`.
3. In the window:
   - Click **Browse** to pick the MSIX (leave empty to auto-use the first `*.msix` in `input\`);
   - Tick **Skip app install / Skip driver install** as needed;
   - Click **Run all (one-click)** (or Install app only / Install driver only / Verify / Environment check).
4. Install actions will **auto-request admin elevation (UAC)** — approve it.
5. The log box scrolls live; when the three Elgato services show `Running`, you are done.
6. **UI language**: use the **Language** drop-down at the top-right to switch between **中文 / English**. The choice is persisted to `%LOCALAPPDATA%\WaveLinkWin10Setup\lang.cfg` and applies to the next launch and the elevated child process. Default follows the OS display language.

> The exe embeds the patch script (`patch_manifest.ps1`). At runtime it needs `input\` (your MSIX); if the official driver MSI is missing under `driver\`, the exe tries to call the sibling `scripts/fetch_driver.bat` to auto-download it from the official CDN. For safety, place the repo's `driver/` next to the exe, or build `dist/` (which already contains `driver/` and `input/`) from source. See FAQ_EN.md "5. GUI installer".

### Build from source
Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed locally. From the repo root run:
```bat
PowerShell -ExecutionPolicy Bypass -File build_exe.ps1
```
Output: `dist/WaveLinkWin10Setup.exe` (the script auto-copies `driver/` and `input/` into `dist/`, ready to ship).

### Prerequisites

- **Windows 10 2004 (19041) or newer** (22H2 / 19045 verified). On **1809/1909**, the app auto-install needs a trusted-cert re-sign (see FAQ_EN.md "Manual re-sign route"); the driver MSI is supported on 1809+. Check: press `Win + R` → type `winver` → Enter.
- **Official Wave Link 3.x MSIX** (you provide it, placed in `input/`).
- Administrator rights (required for app + kernel driver install).
- This repository downloaded locally.

### Step 1: Get the repository

**Option A — Download ZIP (easiest)**
1. On the GitHub page click green **Code → Download ZIP**.
2. Extract to any folder, e.g. `C:\wavelink-win10-driver\`.

**Option B — git clone**
```bat
git clone https://github.com/<your-username>/wavelink-win10-driver.git
cd wavelink-win10-driver
```

### Step 2: Place the official MSIX

Put your official Wave Link 3.x MSIX into the repo's `input\` folder, e.g.:
`input\Elgato.WaveLink_3.2.10.4073_x64_Win10.msix`.
(Source: Elgato official downloader export / Store export / your backup. Not hosted by this repo.)

### Step 3: One-click end-to-end install (recommended)

Open PowerShell as Administrator (or just type `powershell` in the Explorer address bar),
`cd` to the repo root, and run:

```bat
PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1
```

The script will: elevate if needed → enable Developer Mode → patch & repack the MSIX →
install the app unsigned → install the official driver MSI → verify the
`ElgatoVirtUsbAudioEmu / ElgatoUsbAudio / ElgatoUsbAudioks` services are Running. Log: `setup_wavelink_win10.log`.

Optional parameters:

| Parameter | Meaning |
|---|---|
| `-MsixPath <path>` | Explicit MSIX path; omit to auto-use the first `.msix` in `input\` |
| `-SkipApp` | Skip app install (app already present; only (re)install the driver) |
| `-SkipDriver` | Skip driver install (only install the app) |
| `-MinBuild 19041` | Target Win10 floor after patching (default 19041 = 2004) |

Example — driver only (app already installed):
```bat
PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1 -SkipApp
```

> To understand how the script "bypasses the Win11 gate / why unsigned install works", see FAQ_EN.md "How the MSIX Win11 gate is bypassed".

### Step 4: Driver only / reinstall (app already installed)

Open `scripts\`, **right-click** `reinstall_wavelink_driver.bat` → **Run as administrator**.
Silent install (`/qn`), ~tens of seconds, no reboot.

> Manual copy-paste method: see FAQ_EN.md "Manual driver install".

### Step 5: Verify

1. Launch Wave Link 3.x (if you used the one-click script, the app is already installed).
2. Open **Wave Link Settings → Output Routing** and confirm all of these endpoints are visible and selectable:
   `Wave Mic 1–4, Game, Music, Chat Mix, Voice Chat, Browser, SFX, System, Aux 1/2, Aux Mix, Personal Mix, Stream Mix, Recording Mix` (17 total).
3. If all appear, the driver is installed. Detailed evidence: `reports/wavelink_verify_report.md`.

> In the first ~2 minutes you may see `hr=0x88890004` (pipeline init jitter) — benign, settles shortly.

### Step 6: Re-fetch if missing

- **Driver MSI missing**: open `scripts\`, double-click `fetch_driver.bat` to re-download from the official public CDN into `driver\`.
- **App MSIX help**: open `scripts\`, double-click `fetch_app.bat` for placement guidance; drop the official MSIX into `input\` and re-run the one-click script.

## How the research artifacts were produced

| Step | What it does | Artifact / Script |
|---|---|---|
| 0. App bypass | Lower MSIX MinVersion + install unsigned under Developer Mode | `scripts/setup_wavelink_win10.ps1` → `scripts/patch_manifest.ps1` |
| 1. Locate driver | Reverse-engineer `Elgato.BaseClasses.Core.dll` to find the official endpoint; capture the MSI with spoofed Win11 params | `driver/WaveLinkDriver_3.0.0.466_x64.msi` |
| 2. Compatibility check | Read the 4 INF files inside the MSI's embedded CAB; confirm no Win11-only gate | `evidence/inf/*.inf` |
| 3. Install driver | `msiexec /i` (critical: use a native Windows host to avoid the MSYS path-translation trap) | exit code 0, no reboot |
| 4. Verify | Launch Wave Link 3.x; capture logs confirming 17/17 virtual routing endpoints ready | `reports/wavelink_verify_report.md` |

Conclusion: the driver binaries are Win10-compatible (`LaunchConditions` pass, no Win11 decoration in any INF); the original "stuck at Install driver" block (server-side non-delivery) is bypassed.

## Repository Layout

```
wavelink_win10_driver/
├── README.md                      # Chinese version (step-by-step guide)
├── README_EN.md                   # This file (English)
├── FAQ.md                         # Principles / manual steps / FAQ (Chinese)
├── FAQ_EN.md                      # FAQ in English
├── NOTICE                         # Compliance & copyright
├── LICENSE                        # MIT (self-authored only)
├── .gitignore
├── build_exe.ps1                  # Build the GUI installer (dotnet publish self-contained)
├── src/WaveLinkWin10Setup/        # GUI installer source (C#/.NET 8 WinForms; patch_manifest.ps1 embedded as resource)
└── dist/                          # ★ Build output (git-ignored; distributed via Releases)
├── driver/
│   └── WaveLinkDriver_3.0.0.466_x64.msi   # Official public-CDN driver (~3MB)
├── scripts/
│   ├── setup_wavelink_win10.ps1          # ★ End-to-end: patch+install app + install driver + verify
│   ├── patch_manifest.ps1                # ★ Lower MSIX AppxManifest MinVersion + repack (bypass Win11 gate)
│   ├── reinstall_wavelink_driver.bat     # Standalone one-click driver reinstall (admin elevation + msiexec /qn)
│   ├── fetch_driver.bat                  # Re-download MSI from official CDN (binary-free alternative)
│   ├── fetch_app.bat                     # Instructions for obtaining the MSIX (app not hosted here)
│   ├── extract_cab.py / find_cab.py / list_streams.py  # Research: extract MSI embedded CAB
│   ├── patch_manifest.py                 # Research: earlier Python patcher (same principle)
│   ├── envcheck.ps1                      # Research: environment check (admin / MSI path)
│   └── install.ps1                       # Research: cert import + Add-AppxPackage entry
├── reports/
│   ├── wavelink_driver_install_report.md  # Install & INF compatibility report
│   └── wavelink_verify_report.md          # Live verification report (17/17 endpoints)
├── certs/
│   └── WaveLinkPatch.cer                  # Public half of the repack cert (illustrative only, no private key)
├── input/                                # ★ Drop your official MSIX here (*.msix ignored by .gitignore)
│   ├── README.txt
│   └── .gitkeep
└── evidence/
    ├── inf/                               # 4 Elgato INF files (Win10 compatibility proof, © Elgato)
    │   ├── ElgatoUsbAudio.inf
    │   ├── ElgatoUsbAudio_dfu.inf
    │   ├── ElgatoUsbAudioks.inf
    │   └── ElgatoVirtUsbAudioEmu.inf
    ├── duc_win10.json / duc_win11.json    # Device-update-check request/response (Win10 vs Win11)
    ├── pnp.txt                            # PnP virtual device status
    ├── a_log.txt                          # App runtime log excerpt
    ├── msi_install*.log                   # msiexec install logs (success exit code 0)
    ├── extract_log2.txt / envcheck.txt / install_log.txt / exitcode.txt
```

> ★ = core end-to-end automation scripts. The other `.py` / older `.ps1` files are research artifacts kept for reference, not required.

## Compliance & Exclusions

This repo deliberately **excludes** the following Elgato proprietary / large items, keeping only research artifacts and the official public driver:

| Excluded | Size | Reason |
|---|---|---|
| Two MSIX app packages | ~191MB ×2 | Elgato proprietary app, copyright / size |
| `src\` unpacked app DLLs | ~390MB | Elgato proprietary app code |
| Driver binaries `.sys`/`.dll`/`.cat` | — | Provided by the MSI itself; redistributing standalone is a copyright risk |
| `win11_update.xml` | 52MB | Server-response cache |

**Included**: reports, scripts, evidence logs, the official public-CDN driver MSI (3MB), self-signed cert, and the 4 INF texts. The app MSIX is supplied locally by the user (in `input/`, git-ignored) and never enters the repo.

> For a binary-free repo, delete `driver/*.msi` and use `scripts/fetch_driver.bat`.

## Known Limitations

- Step 0's "unsigned app install" relies on Windows 10 **2004 (19041) or newer** Developer Mode `-AllowUnsigned`; 1809/1909 need a trusted-cert re-sign (no .pfx shipped here).
- In the first ~2 minutes `hr=0x88890004` (Thesycon pipeline init jitter) may appear — benign.
- `Found 0 drivers` in the log is a runtime server-update query (Win10 server delivers none), not a local absence — the same log shows `Found current driver version: 3.0.0.466`.
- GUI-level final verification (play audio, confirm meters move) requires manual operation on your side.
- Compatibility is empirically verified only on Windows 10 22H2; 1809+ is theoretical, not per-build tested.

## Automated build & auto-release (GitHub Actions split)

This repo is the **patch repo** — it only builds and publishes the patched installer. The **auto-detection of the latest Elgato MSIX + bundling + releasing** has been moved to a separate repo:
[`wavelink-win10-autorelease`](https://github.com/DRDRDRRDDRDR/wavelink-win10-autorelease) (runs daily at 06:00 UTC; see that repo's README for the mechanism and links).

### This repo's workflow (`.github/workflows/build.yml`)
- Triggers: push a tag (`vX.Y.Z`) or manual `workflow_dispatch`.
- Action: `dotnet publish` builds the self-contained installer exe, bundles it with `driver/` into `wavelink-patch-bundle.zip`, and publishes to this repo's Release.
- This is the "patch artifact" (installer + driver) — it does **not** contain Elgato's MSIX.

### Where is the complete package (with MSIX)?
It is generated automatically by `wavelink-win10-autorelease`: it clones this repo to build the installer, fetches the latest MSIX from the official site, assembles the complete package, and releases it (tag like `wavelink-3.2.10`). Regular users should just download the complete package from that repo.

- Credentials: GitHub built-in `GITHUB_TOKEN` (`contents: write`) — no personal PAT required.
- Compliance: the MSIX is Elgato proprietary; it is only fetched from the official CDN at build time and bundled into the Release archive — never re-hosted standalone or modified.

> For an equivalent local build, see `build_exe.ps1`.

## References

- Install report: `reports/wavelink_driver_install_report.md`
- Verify report: `reports/wavelink_verify_report.md`
- FAQ & manual steps: [FAQ_EN.md](./FAQ_EN.md)
- Official driver CDN: `https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi`
