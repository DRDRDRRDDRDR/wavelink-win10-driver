# Force-install Elgato Wave Link 3.x Driver on Windows 10 — Research / Reproduction Kit

> ⚠️ **Disclaimer**: This project is not affiliated with Elgato / Corsair. It is for technical research, compatibility verification, and local reproduction only.
> All Elgato proprietary assets (driver MSI, INF) are copyrighted by Elgato and must be used in accordance with their license.
> Not intended for commercial use or for circumventing normal product licensing. See [`NOTICE`](./NOTICE).
> 中文说明见 [README.md](./README.md)。

---

## Abstract

Elgato Wave Link 3.x is officially Windows 11 only. This repository provides the full set of artifacts needed to bypass the "server does not push the driver" block and install + verify the official audio driver on **Windows 10** — including reverse-engineering evidence, compatibility analysis, install logs, and a one-click reinstall script. **No Elgato proprietary app binaries are included**; only research artifacts and the official public-CDN driver (~3 MB).

## Background

Elgato Wave Link 3.x targets Windows 11 only (MSIX `MinVersion=22000`). On **Windows 10 22H2 (19045)** the app hangs at "Install driver". Root cause: at runtime Wave Link queries the server `device-update-check.php`, and for `osVersion=10.0.19045.0` the server returns an empty list (`appDevices:[]`), so the audio driver is never delivered.

Reverse-engineering and live testing confirm the block exists in only two layers (MSIX version gate + server-side driver delivery), while **the driver binaries themselves are open to Windows 10**. This repo ships the full artifact set to bypass the block, install the driver, and verify it works.

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
- **Theoretically supported**: Windows 10 **1809 (17763) and every later build** (1809 / 1903 / 1909 / 2004 / 20H1 / 20H2 / 21H1 / 21H2 / 22H2). The main drivers (UsbAudio, UsbAudioks) require 1809+; the VirtUsbAudioEmu sub-driver can go as low as 1803.
- **No Win11 hard-lock**: none of the 4 INF decorations carry a 22000+ (Windows 11) exclusive gate — they are open to the entire Windows 10 line.
- **Not supported**: Windows 10 1709 (16299) and earlier (below 17134).
- **Prerequisite**: the Wave Link 3.x app itself must first have its MSIX Win11 gate bypassed (out of scope for this repo); this repo only solves the "driver not delivered" step.

> Note: 1809+ is **theoretical support** derived from the INF OS decorations; only 22H2 has been empirically tested. If you succeed on another Windows 10 build, please open an issue/PR to extend the verified matrix.

## Overview

| Step | What it does | Artifact |
|---|---|---|
| 1. Locate driver | Reverse-engineer `Elgato.BaseClasses.Core.dll` to find the official endpoint; capture the MSI with spoofed Win11 params | `driver/WaveLinkDriver_3.0.0.466_x64.msi` |
| 2. Compatibility check | Read the 4 INF files inside the MSI's embedded CAB; confirm no Win11-only gate | `evidence/inf/*.inf` |
| 3. Install | `msiexec /i` (critical: use a native Windows host to avoid the MSYS path-translation trap) | exit code 0, no reboot |
| 4. Verify | Launch the patched Wave Link 3.x; capture logs confirming 17/17 virtual routing endpoints ready | `reports/wavelink_verify_report.md` |

Conclusion: the driver binaries are Win10-compatible (`LaunchConditions` pass, no Win11 decoration in any INF); the original "stuck at Install driver" block (server-side non-delivery) is bypassed.

## Repository Layout

```
wavelink_win10_driver/
├── README.md                      # Chinese version
├── README_EN.md                   # This file (English)
├── NOTICE                         # Compliance & copyright
├── LICENSE                        # MIT (self-authored only)
├── .gitignore
├── driver/
│   └── WaveLinkDriver_3.0.0.466_x64.msi   # Official public-CDN driver (~3MB)
├── scripts/
│   ├── reinstall_wavelink_driver.bat      # One-click reinstall (admin elevation + msiexec /qn)
│   ├── fetch_driver.bat                   # Re-download MSI from official CDN (binary-free alternative)
│   ├── extract_cab.py                     # Extract MSI embedded CAB via olefile
│   ├── find_cab.py                        # Locate the Media.cab stream inside the MSI
│   ├── list_streams.py                    # List OLE compound-file streams
│   ├── patch_manifest.py                  # Patch MSIX AppxManifest MinVersion (bypass Win11 gate)
│   ├── envcheck.ps1                       # Environment check (admin / MSI path)
│   └── install.ps1                        # Native-host install entry point
├── reports/
│   ├── wavelink_driver_install_report.md  # Install & INF compatibility report
│   └── wavelink_verify_report.md          # Live verification report (17/17 endpoints)
├── certs/
│   └── WaveLinkPatch.cer                  # Self-signed cert for MSIX repack (illustrative only, not required)
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

## Step-by-Step Guide

> This guide assumes you have already bypassed the MSIX Win11 gate and installed the Wave Link 3.x app on Windows 10. This repo solves only the "driver not delivered" step.

### Prerequisites

- **Windows 10 1809 (17763) or newer** (22H2 / 19045 verified; 1809+ theoretical — see Compatibility Scope). Check: press `Win + R` → type `winver` → Enter.
- Wave Link 3.x installed (app launches but hangs at "Install driver" / routing endpoints missing).
- Administrator rights (required to install a kernel driver).
- This repository downloaded locally (see Step 1).

### Step 1: Get the repository

**Option A — Download ZIP (easiest)**
1. On the GitHub page click green **Code → Download ZIP**.
2. Extract to any folder, e.g. `C:\wavelink-win10-driver\`.

**Option B — git clone**
```bat
git clone https://github.com/<your-username>/wavelink-win10-driver.git
cd wavelink-win10-driver
```

### Step 2: Install the driver

#### Option A: One-click (recommended)
1. Open the `scripts\` folder.
2. **Right-click** `reinstall_wavelink_driver.bat` → **Run as administrator**.
3. The script auto-detects admin rights; if not elevated it triggers a UAC prompt. Silent install (`/qn`), takes ~tens of seconds.
4. When the window returns to the prompt, done. No reboot needed.

#### Option B: Manual (copy-paste)
1. Open Command Prompt (CMD) **as administrator**: press `Win`, type `cmd`, right-click → Run as administrator.
2. Run line by line (adjust the path to where you extracted):
```bat
cd /d "C:\wavelink-win10-driver\driver"
msiexec /i WaveLinkDriver_3.0.0.466_x64.msi /qn /norestart /l*v msi_install.log
```
3. Wait for the command to return (no progress bar); the tail of `msi_install.log` should show `Return Value 0`.

> ⚠️ **Critical gotcha**: Always launch `msiexec` from a native Windows host (cmd.exe / PowerShell). Under **Git Bash / MSYS** the path `C:\Users\...` is rewritten to `/Users\...`, causing server error `Note 1314` and exit code **83**. Use CMD or `scripts/install.ps1`.

### Step 3: Verify

1. Launch Wave Link 3.x (requires the MSIX repack step, out of scope).
2. Open **Wave Link Settings → Output Routing** and confirm all of these endpoints are visible and selectable:
   `Wave Mic 1–4, Game, Music, Chat Mix, Voice Chat, Browser, SFX, System, Aux 1/2, Aux Mix, Personal Mix, Stream Mix, Recording Mix` (17 total).
3. If all appear, the driver is installed. Detailed evidence: `reports/wavelink_verify_report.md`.

> In the first ~2 minutes you may see `hr=0x88890004` (pipeline init jitter) — benign, settles shortly.

### Step 4: Re-fetch the driver if missing

If you deleted `driver/*.msi` or want a guaranteed original:
1. Open `scripts\`.
2. Double-click `fetch_driver.bat` (or right-click → Run as administrator).
3. It re-downloads from the official public CDN `https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi` into `driver\`.
   - Internet required; if your network cannot reach the CDN directly, configure a proxy before running.

### FAQ

**Q1: The install script flashes by / does nothing?**
A: Must be run inside `scripts\` and as administrator. Run `scripts\reinstall_wavelink_driver.bat` manually from CMD to see full output.

**Q2: msiexec exit code 83?**
A: Almost always the Git Bash/MSYS path translation. Use native CMD or `install.ps1`, keeping the path as `C:\...`. See the Step 2 warning.

**Q3: Log shows `Found 0 drivers` — did it fail?**
A: No. That is the runtime querying the server for updates (Win10 server delivers none), not a local absence. The same log shows `Found current driver version: 3.0.0.466`, proving the local driver is present.

**Q4: Can the repo be completely binary-free?**
A: Yes. Delete `driver/*.msi` and use `scripts/fetch_driver.bat` instead (see Step 4).

**Q5: I'm on Windows 10 1809 / 1909 / 21H1 — will it work?**
A: Based on the INF OS decorations, the driver install layer is **theoretically supported on 1809 (17763) and above** (VirtUsbAudioEmu can go to 1803). Only 22H2 is empirically verified; try it on older builds per this guide and report back. 1709 (16299) and earlier are not supported.

## Compliance & Exclusions

This repo deliberately **excludes** the following Elgato proprietary / large items, keeping only research artifacts and the official public driver:

| Excluded | Size | Reason |
|---|---|---|
| Two MSIX app packages | ~191MB ×2 | Elgato proprietary app, copyright / size |
| `src\` unpacked app DLLs | ~390MB | Elgato proprietary app code |
| Driver binaries `.sys`/`.dll`/`.cat` | — | Provided by the MSI itself; redistributing standalone is a copyright risk |
| `win11_update.xml` | 52MB | Server-response cache |

**Included**: reports, scripts, evidence logs, the official public-CDN driver MSI (3MB), self-signed cert, and the 4 INF texts.

> For a binary-free repo, delete `driver/*.msi` and use `scripts/fetch_driver.bat`.

## Known Limitations

- In the first ~2 minutes `hr=0x88890004` (Thesycon pipeline init jitter) may appear — benign.
- `Found 0 drivers` in the log is a runtime server-update query (Win10 server delivers none), not a local absence — the same log shows `Found current driver version: 3.0.0.466`.
- GUI-level final verification (play audio, confirm meters move) requires manual operation on your side.
- This repo does not ship the repacked Wave Link app (copyright & size); it only closes the driver-install loop.
- Compatibility is empirically verified only on Windows 10 22H2; 1809+ is theoretical, not per-build tested.

## References

- Install report: `reports/wavelink_driver_install_report.md`
- Verify report: `reports/wavelink_verify_report.md`
- Official driver CDN: `https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi`
