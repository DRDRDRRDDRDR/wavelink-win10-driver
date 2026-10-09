# Wave Link 3.x on Windows 10: installer and patch source

Elgato ships Wave Link 3 for Windows as a Windows 11–only package: the MSIX `AppxManifest.xml` declares `MinVersion="10.0.22000.0"`, so the AppX installer refuses it on Windows 10. Even after it installs, the app asks Elgato's server for the audio driver at startup and the server returns an empty device list for the `osVersion` that Windows 10 reports. The UI sits on "Install driver" and never moves.

Both layers have to be handled separately:

1. Lower the manifest's `MinVersion` to a Windows 10 build, rewrite the `<Identity>` Publisher, drop the now-invalid `AppxSignature.p7x`, repack, install.
2. Skip the server-side delivery path entirely and install the official driver MSI straight into the system.

The driver binaries themselves carry no Windows 11 gate. That is what makes the whole thing work.

This is the **patch repository**: installer source, the official driver MSI, scripts, reports, docs. Detecting new upstream releases and publishing bundles lives in the companion repo [`wavelink-win10-autorelease`](https://github.com/DRDRDRRDDRDR/wavelink-win10-autorelease).

Not affiliated with Elgato or Corsair. Asset licensing is covered in [NOTICE](./NOTICE).

## Upstream state

Queried against the Elgato Zendesk API (`elgato.zendesk.com`) on 2026-10-09: the newest Windows build is **Wave Link 3.3**, released 16 September 2026, article id `50957117157265`.

- x64 package: `https://edge.elgato.com/egc/windows/ewlw/3.3/Stable/Elgato.WaveLink_3.3.0.4529_x64.msix`
- 195,150,702 bytes (186.11 MB), SHA256 `50DDDC68ACBB3C95F193B9A3A56F1EDE1C7D575E91423D5BBA597ECAD88B04C9`
- Wave Link 3 is free and works with non-Elgato microphones. Wave Link 2 is end of life.
- 3.3.0.4529 supersedes the previously bundled 3.2.10.4073.

What changed in 3.3 is listed in [FAQ_EN.md](./FAQ_EN.md).

## Compatibility

The driver MSI contains four INF packages. Their OS decorations set the floors (text copies are kept under `evidence/inf/`):

| INF | Minimum Windows 10 | Build |
|---|---|---|
| `ElgatoUsbAudio.inf` | 1809 | 17763 |
| `ElgatoUsbAudioks.inf` | 1809 | 17763 |
| `ElgatoVirtUsbAudioEmu.inf` | 1803 | 17134 |
| `ElgatoUsbAudio_dfu.inf` | none | — |

| Target | Status |
|---|---|
| Windows 10 22H2 (19045) | Verified |
| Windows 10 1809 (17763) through 22H2 | Supported in theory; not tested build by build |
| Windows 10 1709 (16299) and earlier | Not supported (below 17134) |
| Windows 11 | Natively supported by Elgato; this project is unnecessary |
| A 22000+ gate | None of the four INFs carries one |

To check your build: `Win + R` → `winver` → Enter.

The driver side works on 1809 and up. The app side depends on the OS build:

- **2004 (19041) and newer** supports `Add-AppxPackage -AllowUnsigned`, which is the route the PowerShell script takes.
- **1809 / 1909** has no `-AllowUnsigned`. The repacked MSIX must be signed with a trusted certificate and installed normally. The GUI installer does this itself: it creates a self-signed certificate, trusts it into `LocalMachine\Root`, and signs with `signtool`. It does not depend on `-AllowUnsigned` at all.

`-MinBuild` defaults to 19041; that is the OS floor written into the manifest. It is a separate thing from the install route: the floor controls how old a build will accept the package, while `-AllowUnsigned` itself requires 2004 (19041) or newer. Lowering the floor does not give 1809 the ability to install unsigned.

## The two repositories

| Repository | Role | Output |
|---|---|---|
| [`wavelink-win10-driver`](https://github.com/DRDRDRRDDRDR/wavelink-win10-driver) (this one) | Installer source, official driver, scripts, docs | `wavelink-patch-bundle.zip` (installer + driver, no MSIX) |
| [`wavelink-win10-autorelease`](https://github.com/DRDRDRRDDRDR/wavelink-win10-autorelease) | Detects upstream releases daily, downloads the MSIX, builds the installer from the `patch/` source copy inside itself, publishes the full bundle | `wavelink-win10-complete.zip` (installer + driver + scripts + MSIX) |

Both workflows use GitHub's built-in `GITHUB_TOKEN`. No personal access token is involved.

If you just want Wave Link running, get the complete package from the auto-release repo and ignore the build machinery here.

## Get the complete package and install

1. Open the [Releases page of `wavelink-win10-autorelease`](https://github.com/DRDRDRRDDRDR/wavelink-win10-autorelease/releases) and download the newest `wavelink-win10-complete.zip` (tag looks like `wavelink-3.3`).
2. Extract it anywhere. You get `WaveLinkWin10Setup.exe`, `driver\`, `scripts\`, and `input\` (holding the official MSIX).
3. **Turn on Developer Mode**: `Settings` → `Update & Security` → `For developers` → Developer mode. The PowerShell script route installs unsigned and needs this; without it, the install fails outright. The GUI installer signs its own package and does not require it, but leaving it on causes no harm.
4. Right-click `WaveLinkWin10Setup.exe` → **Run as administrator**. Install actions request UAC on their own; running elevated just saves one confirmation.
5. Click **Env Check (dry-run)** first. It reports the OS build, admin status, and whether the MSIX in `input\` and the MSI in `driver\` are findable.
6. Click **Run All (One-Click)**. It patches and repacks, signs and installs the app, installs the driver MSI, then verifies.
7. See [Verification](#verification).

If you already have your own official MSIX, drop it into `input\` (any filename, `.msix` extension), then either pick it with **Browse** or leave the field empty to auto-use the first `*.msix` in `input\`.

## GUI installer

`WaveLinkWin10Setup.exe` is a C# / .NET 8 WinForms application published as a self-contained single-file win-x64 binary, roughly 150 MB. The target machine needs no .NET runtime.

| Control | What it does |
|---|---|
| MSIX path field + **Browse** | Points at your official MSIX; empty means "first file in `input\`" |
| **Skip app install** / **Skip driver install** | Run only half of the flow |
| Min Win10 build | The `MinVersion` written when patching; default 19041 |
| **Run All (One-Click)** | Full flow: patch → app → driver → verify |
| **App Only** / **Driver Only** | Single steps |
| **Verify** | Verification only; changes nothing |
| **Env Check (dry-run)** | Read-only; does not need admin |
| **Check & Update Wave Link** | Queries Elgato for the newest MSIX, compares it against the installed version, downloads it, and starts an elevated install |
| Log box | Live output of every command and result |
| Progress indicator | Marquee while a step is running, percentage when it finishes |
| **Language** drop-down | 中文 / English, persisted to `%LOCALAPPDATA%\WaveLinkWin10Setup\lang.cfg` and inherited by the elevated child process |

When not elevated, install actions relaunch the same exe through `runas` with the original arguments (`--run`, `--msix`, `--minbuild`, `--skipapp`, `--skipdriver`). `patch_manifest.ps1` is compiled into the exe as an embedded resource, so patching works without a `scripts\` folder next to it.

The interface uses a Telegram-inspired light/dark theme: rounded cards, flat accent buttons, hover transitions, and a press ripple.

There is one command-line flag. `--check` runs the read-only environment check, printing to the console and writing `env_check.txt` next to the exe:

```bat
WaveLinkWin10Setup.exe --check
```

## Build from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). From the repository root:

```bat
PowerShell -ExecutionPolicy Bypass -File build_exe.ps1
```

That script runs:

```bat
dotnet publish src/WaveLinkWin10Setup/WaveLinkWin10Setup.csproj -c Release -r win-x64 ^
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:PublishTrimmed=false -p:DebugType=None -o dist
```

The output is `dist\WaveLinkWin10Setup.exe`. The `BundleAssetsAfterPublish` target in the csproj copies `driver\` and `scripts\` into `dist\` after publishing, and the script adds `input\`, so `dist\` is ready to hand out as-is.

`.github/workflows/build.yml` runs on a `v*` tag or manual dispatch, builds the exe, and publishes `wavelink-patch-bundle.zip` (installer + `driver\`) to this repo's Releases. A second workflow, `.github/workflows/release.yml`, runs on a `wavelink-*` tag or manual dispatch and publishes the installer alone as `wavelink-win10-installer.zip`.

## PowerShell one-liner route

`scripts\setup_wavelink_win10.ps1` runs the same flow without a GUI. It self-elevates when needed and writes a transcript to `setup_wavelink_win10.log` in the repository root.

```bat
PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1
```

| Parameter | Meaning |
|---|---|
| `-MsixPath <path>` | Explicit MSIX; omitted means the first `.msix` in `input\` |
| `-SkipApp` | Skip the app; only install or reinstall the driver |
| `-SkipDriver` | Skip the driver; only install the app |
| `-MinBuild 19041` | Windows 10 floor written into the patched manifest; default 19041 |

Driver only:

```bat
PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1 -SkipApp
```

This script's verification step reads service state for `ElgatoVirtUsbAudioEmu`, `ElgatoUsbAudio`, and `ElgatoUsbAudioks`, and ends with a warning if any is not `Running`. Kernel-mode driver services register with the SCM only on PnP enumeration, so a freshly installed driver showing `Stopped` or not appearing at all is expected. Judge the install by the driver store instead, as described in the next section.

For a driver-only reinstall there is also `scripts\reinstall_wavelink_driver.bat`: right-click → Run as administrator. It calls `msiexec /i ... /qn /norestart` and writes `scripts\msi_reinstall.log`. That bat expects `WaveLinkDriver_3.0.0.466_x64.msi` to sit beside it, and it does not look in `driver\`, so run it from inside `driver\` or copy the MSI next to it first.

If the driver MSI is missing, `scripts\fetch_driver.bat` re-downloads it from the official CDN into `driver\`.

## Verification

Two independent checks, both read directly from the system rather than from log wording.

**Driver**: are the INF packages staged in the Windows driver store?

```bat
pnputil /enum-drivers
```

Look for `elgatousbaudio.inf`, `elgatousbaudioks.inf`, and `elgatovirtusbaudioemu.inf`. Driver-store writes happen synchronously during install, unlike SCM service registration which lags, so this is the strongest evidence available.

**App**:

```powershell
Get-AppxPackage -Name Elgato.WaveLink | Select-Object Name, Version
```

A version means the package is installed.

The installer's **Verify** button runs both of these and prints service state alongside. Services that are `Stopped` or not yet registered are reported, not treated as failure.

The final confirmation happens inside Wave Link. Open `Settings → Output Routing` and check that all 17 endpoints are present and selectable:

`Wave Mic 1–4`, `Game`, `Music`, `Chat Mix`, `Voice Chat`, `Browser`, `SFX`, `System`, `Aux 1/2`, `Aux Mix`, `Personal Mix`, `Stream Mix`, `Recording Mix`, 17 in total

This part needs a human at the machine; no script can do it. The recorded evidence is in `reports/wavelink_verify_report.md`, and the INF compatibility findings are in `reports/wavelink_driver_install_report.md`.

## Known limitations

- Only Windows 10 22H2 (19045) has been tested. 1809 and up is a theoretical range derived from the INF OS decorations, never verified build by build.
- For about two minutes after launch, `hr=0x88890004` (a Thesycon pipeline init race) can appear in the Wave Link log. The devices are already `started` and it clears on its own.
- `Found 0 drivers` in the app log is the runtime update query; Windows 10 gets nothing back from the server. It says nothing about the local driver, and the same log records `Found current driver version: 3.0.0.466`.
- Launching `msiexec` from Git Bash / MSYS rewrites `C:\...` to `/Users/...` and the install fails with exit code **83**. Use `cmd.exe` or PowerShell. The GUI installer is unaffected.
- Whether audio actually plays and streaming mixes work needs a real listening test in Wave Link.
- No `.pfx` private key ships here. `certs\WaveLinkPatch.cer` is the public half of the certificate used on the machine that originally repacked, and is useless for signing anyone else's MSIX.

## Repository layout

```
wavelink-win10-driver/
├── README.md / README_EN.md
├── FAQ.md / FAQ_EN.md
├── NOTICE                      compliance and copyright
├── LICENSE                     MIT (self-authored scripts and docs only)
├── .gitignore
├── build_exe.ps1               build the self-contained installer
├── .github/workflows/
│   ├── build.yml               v* tag or manual → wavelink-patch-bundle.zip
│   └── release.yml             wavelink-* tag or manual → wavelink-win10-installer.zip
├── src/WaveLinkWin10Setup/
│   ├── WaveLinkWin10Setup.csproj
│   ├── App.manifest
│   ├── Program.cs              entry point; --check goes headless
│   ├── MainForm.cs             window and controls
│   ├── Installer.cs            patch / install app / install driver / verify
│   ├── Updater.cs              query Elgato Zendesk and download the newest MSIX
│   ├── Lang.cs                 zh/en string table
│   ├── EnvironmentCheck.cs     read-only check behind --check
│   └── Resources/patch_manifest.ps1   embedded patch script
├── driver/
│   ├── WaveLinkDriver_3.0.0.466_x64.msi   official public-CDN driver (2.91 MB)
│   └── elgato/                            the 4 signed driver packages, used by the pnputil fallback
├── scripts/
│   ├── setup_wavelink_win10.ps1       one-shot flow (patch + app + driver + verify)
│   ├── patch_manifest.ps1             MinVersion/Publisher rewrite, signature removal, repack
│   ├── reinstall_wavelink_driver.bat  standalone driver reinstall
│   ├── fetch_driver.bat               re-download the MSI from the official CDN
│   ├── fetch_app.bat                  MSIX placement instructions
│   ├── install.ps1                    cert import + Add-AppxPackage example
│   ├── envcheck.ps1                   environment check
│   ├── patch_manifest.py              earlier Python patcher, same idea
│   └── extract_cab.py / find_cab.py / list_streams.py   pull the CAB out of the MSI
├── reports/
│   ├── wavelink_driver_install_report.md
│   └── wavelink_verify_report.md
├── certs/WaveLinkPatch.cer
├── input/                      where your official MSIX goes (*.msix is git-ignored)
│   ├── README.txt
│   └── .gitkeep
└── evidence/
    ├── inf/                    the 4 Elgato INF files (© Elgato)
    ├── a_log.txt               app runtime log excerpt
    ├── duc_win10.json / duc_win11.json   device-update-check request and response
    ├── pnp.txt                 PnP virtual device status
    ├── envcheck.txt / exitcode.txt / extract_log2.txt
    ├── install_log.txt
    └── msi_install*.log        msiexec install logs
```

The `.py` files and the older `envcheck.ps1` / `install.ps1` under `scripts\` are research artifacts. The working flow does not need them.

## Compliance

Three kinds of Elgato proprietary asset, all copyright Elgato:

| Asset | How it appears here |
|---|---|
| `driver/WaveLinkDriver_3.0.0.466_x64.msi` | Kept as downloaded from `https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi` |
| The four driver packages under `driver/elgato/` | Extracted from the official MSI |
| The four INF texts under `evidence/inf/` | Same source, for reading |

The Wave Link app MSIX never enters this repository: `input/*.msix` is git-ignored, and the auto-release repo fetches it once from the official CDN at build time and bundles it inside the release archive. Details in [`NOTICE`](./NOTICE).

For a binary-free repository, delete `driver/*.msi` and `driver/elgato/` and rely on `scripts\fetch_driver.bat`.

Self-authored scripts and documentation are MIT. See `LICENSE`.

## References

- [FAQ_EN.md](./FAQ_EN.md): bypass mechanics, manual re-sign route, manual driver install, common problems
- `reports/wavelink_driver_install_report.md`: driver install and INF compatibility findings
- `reports/wavelink_verify_report.md`: live verification, 17/17 endpoints
- Official driver CDN: `https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi`
- Chinese version: [README.md](./README.md)