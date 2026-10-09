# FAQ: mechanism, manual steps, and problems people hit

If you are using the one-click installer, skip this and read [README_EN.md](./README_EN.md). What follows is what patching actually changes, how to do each step without the scripts, how updates work, and the answers to questions that come up in practice.

## 1. What the patch changes

Wave Link 3.x ships with `MinVersion="10.0.22000.0"` in its `AppxManifest.xml` (22000 is the first Windows 11 build), and the Windows 10 AppX installer refuses it on that basis. `scripts\patch_manifest.ps1` does four things:

1. **Unpack**: `makeappx unpack` when it is available, falling back to .NET's `ZipFile::ExtractToDirectory`. An MSIX is a zip.
2. **Edit the manifest**: replace `MinVersion="10.0.22000.0"` with `MinVersion="10.0.<MinBuild>.0"`, `MinBuild` defaulting to 19041. In the same pass, rewrite the `<Identity>` `Publisher` to `CN=WaveLinkPatch`.
3. **Delete `AppxSignature.p7x`**: the manifest changed, so the original signature cannot possibly validate. Leaving it in only turns a missing signature into a signature error.
4. **Repack**: `makeappx pack` or `ZipFile::CreateFromDirectory`, default output `<input dir>\WaveLink_Win10_patched.msix`.

The result is unsigned. Getting it installed depends on the OS build:

- Windows 10 **2004 (19041) and newer** has `Add-AppxPackage -AllowUnsigned`, which installs it under Developer Mode with no certificate involved. `scripts\setup_wavelink_win10.ps1` takes this route.
- **1809 / 1909** has no such parameter. The GUI installer signs instead: it creates a self-signed certificate (FriendlyName `WaveLinkPatch`, DigitalSignature key usage, code-signing EKU), trusts it with `certutil -addstore -ent -f Root` into `LocalMachine\Root`, and runs `signtool sign /fd SHA256 /sha1 <thumbprint>`. `Add-AppxPackage` only requires a trusted signature. It does not require Elgato to be the issuer.

The signing tool is located in this order: an existing `signtool.exe` under `C:\Program Files (x86)\Windows Kits\10\bin` or `C:\Program Files\Windows Kits\10\bin`; failing that, the redistributable `Microsoft.Windows.SDK.BuildTools` package (version `10.0.28000.2705`) from NuGet, whose x64 tools are extracted into `%LOCALAPPDATA%\WaveLinkWin10Setup\signtool\` and reused afterwards. The target machine needs no Windows SDK, and that download happens once.

The `Publisher` has to match the signing subject, which is why step 2 touches `<Identity>` at all. The script replaces the main `<Identity>` occurrence only; if the match count is not exactly 1 it prints a warning and carries on.

## 2. Manual re-sign route

Use this when the OS lacks `-AllowUnsigned`, or when you want to sign with your own certificate:

```bat
:: 1) Patch, producing input\WaveLink_Win10_patched.msix
PowerShell -ExecutionPolicy Bypass -File scripts\patch_manifest.ps1 -InputMsix input\your.msix

:: 2) Sign with your .pfx (needs the Windows SDK signtool)
signtool sign /fd SHA256 /f your-cert.pfx /p password input\WaveLink_Win10_patched.msix

:: 3) Import the public cert into the trusted root, then install
Import-Certificate -FilePath your-cert.cer -CertStoreLocation Cert:\LocalMachine\TrustedRoot
Add-AppxPackage input\WaveLink_Win10_patched.msix
```

`scripts\install.ps1` shows the same shape, but it was written for the machine where the original work happened and contains that machine's absolute paths. Fix the paths before reusing it.

No `.pfx` private key ships here. `certs\WaveLinkPatch.cer` is only the public half of the certificate from that machine; without the matching private key it cannot sign anyone else's MSIX.

## 3. Manual driver install

1. Open an **administrator** Command Prompt: `Win`, type `cmd`, right-click → Run as administrator.
2. Run, adjusting the path:

```bat
cd /d "C:\wavelink-win10-driver\driver"
msiexec /i WaveLinkDriver_3.0.0.466_x64.msi /qn /norestart /l*v msi_install.log
```

3. The tail of `msi_install.log` should read `Return Value 0`. 3010 is also a success code; it means a reboot is pending.

**Do not launch msiexec from Git Bash or MSYS.** They rewrite `C:\Users\...` into `/Users/...` before passing it along, the server reports `Note 1314` and `MainEngineThread returning 2`, and the install fails with exit code **83**. Use `cmd.exe` or PowerShell and keep the path in `C:\...` form.

The installer does not use the MSI exit code as its only criterion. Success is `msiexec` returning 0 or 3010, **or** the three services `ElgatoVirtUsbAudioEmu`, `ElgatoUsbAudio`, and `ElgatoUsbAudioks` being present in the SCM. Only when neither holds does it fall back to running `pnputil /add-driver "<inf>" /install` over every `.inf` under `driver\elgato\`. If that also fails, it reports the real `msiexec` exit code rather than a placeholder.

## 4. Checking for and installing a new upstream version

The **Check & Update Wave Link** button in the GUI installer runs the full sequence:

1. Query the release-notes article list on `elgato.zendesk.com` and pick the Windows article with the highest version.
2. Extract the official CDN MSIX direct link from that article body with a regex.
3. Read the installed version via `Get-AppxPackage -Name Elgato.WaveLink` and compare; if it is already at least as new, stop.
4. Otherwise download into `input\` (about 180 MB, with a progress line every 5 MB) and then start an elevated install.

There is a size check after the download: anything under 100 MB is treated as a blocked or truncated response and raises an error, so a bad file never reaches the installer.

To do the same by hand: find the newest Windows MSIX link in Elgato's release notes, download it into `input\`, then click **Run All (One-Click)**, or run:

```bat
PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1 -MsixPath input\Elgato.WaveLink_3.3.0.4529_x64.msix
```

The auto-release repo `wavelink-win10-autorelease` does the same thing daily at 06:00 UTC and publishes a Release when the version moves, tagged like `wavelink-3.3`. It records the released version in `wavelink-app-version.txt` and skips re-releasing the same one.

## 5. Why the auto-release was stuck on 3.2.10

The detection script read the version out of the article title with the regex `Elgato Wave Link (\d+\.\d+\.\d+) \(Windows\) Release Notes`, which requires three numeric components.

Elgato titled the 3.3 article "Elgato Wave Link 3.3 (Windows) Release Notes". Two components. The regex did not match, the article was skipped entirely, and the script went on believing 3.2.10 was current. The auto-release sat on 3.2.10 and never moved.

Both `scripts/detect-wavelink-msix.ps1` and the installer's `Updater.cs` now use `\d+(?:\.\d+)*`, and the version comparison walks however many components a version has. Two-, three-, and four-part versions are all detected and compared correctly now.

## 6. What changed in Wave Link 3.3

Windows 3.3 (3.3.0.4529, released 16 September 2026):

- Corsair VOXARA USB condenser microphone support, including Voice Focus and RGB modes.
- Fixed audio effects reordering killing input sound until the effects were disabled or Wave Link was restarted.
- Fixed a Wave Link crash on Wave XLR Pro during the hardware/software mixer transition.

3.3.0.4529 supersedes the previously bundled 3.2.10.4073.

## 7. Does the driver need updating when the app updates

No. The driver and the app are on separate version lines. The driver has been `WaveLinkDriver_3.0.0.466_x64.msi` throughout; Elgato has not published a new one and this project has not modified it.

Upgrading the app normally leaves the driver in place. If it does get cleaned up, reinstall it:

```bat
PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1 -SkipApp
```

Or run `scripts\reinstall_wavelink_driver.bat`. That one requires the driver MSI in the same folder; it does not look in `driver\`.

The 3.3 changes are all on the app side: an effects-chain bug and a crash, with nothing touching the driver. Moving to 3.3 needs no driver work. If audio routing misbehaves after the 3.3 upgrade, check the driver store as described in [README_EN.md](./README_EN.md) before suspecting the driver version.

## 8. FAQ

**The one-click script flashes and closes, apparently doing nothing?**
A: Run it manually from an elevated PowerShell at the repository root: `PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1`, and read the full output. The script also writes `setup_wavelink_win10.log`. Some antivirus products block Developer Mode changes or AppX installs and need a temporary exception.

**`Add-AppxPackage` says `-AllowUnsigned` is not supported.**
A: That parameter needs Windows 10 2004 (19041) or newer with Developer Mode on. On 1809 / 1909, use the manual re-sign route in section 2, or use the GUI installer, which signs the package itself and never calls that parameter.

**msiexec exit code 83.**
A: Almost always Git Bash / MSYS path translation. Switch to native `cmd.exe` or PowerShell and keep the path in `C:\...` form.

**The log says `Found 0 drivers`. Did the install fail?**
A: No. That is the app querying the server for updates, and Windows 10 gets nothing back. The same log records `Found current driver version: 3.0.0.466`, which confirms the local driver is in place. For the real answer, check whether `pnputil /enum-drivers` lists those three INF names.

**The services show `Stopped` after install, or are not listed at all. Failure?**
A: No. Kernel-mode driver services register with the SCM only on PnP enumeration, so querying right after the install cannot see them. They start on demand. The criterion that matters is whether `elgatousbaudio.inf`, `elgatousbaudioks.inf`, and `elgatovirtusbaudioemu.inf` are staged in the driver store.

**Why is the driver store the source of truth rather than the SCM service state?**
A: Driver-store staging is written synchronously during the install. SCM registration lags until the device is actually enumerated. Judging by the lagging signal produces false failures.

**I am on Windows 10 1809 / 1909 / 21H1. Will this work?**
A: The driver side works on 1809 (17763) and up; the `ElgatoVirtUsbAudioEmu` sub-driver reaches down to 1803. The app side depends on the build: 2004 and newer can install unsigned, 1809 and 1909 must re-sign. Only 22H2 has been tested; verify on your own build.

**Does it work on Windows 11?**
A: Yes, but there is no reason to use it. Windows 11 is the natively supported target, so install the official package. Everything here exists for Windows 10.

**Can I install only the driver and leave the app alone?**
A: Yes. Tick **Skip app install** in the GUI, or pass `-SkipApp` on the command line.

**Does the exe need .NET installed first?**
A: No. It is a self-contained single file with the .NET 8 runtime inside.

**I downloaded only the standalone exe from Releases and there is no `driver\` or `input\` next to it.**
A: Put your official MSIX into an `input\` folder beside the exe. When the driver MSI is missing from `driver\`, the exe tries to call the sibling `scripts\fetch_driver.bat` to pull it from the official CDN; if you only have the bare exe with no `scripts\`, that does not trigger, so place `WaveLinkDriver_3.0.0.466_x64.msi` into a `driver\` folder next to the exe yourself. The least fiddly option is to download the whole zip and drop the exe into it.

**What is the difference between the exe and the PowerShell script?**
A: Same flow: MSIX in, patch, app, driver, verify. Two real differences. The exe has a window with a live log, a progress indicator, and automatic elevation, and it signs with a self-signed certificate so it also works on 1809 / 1909. The script is command-line only and installs unsigned via `-AllowUnsigned`, so it needs 2004 or newer.

**Why is the exe 150 MB?**
A: Self-contained publishing embeds the whole .NET 8 runtime. That is the trade for not requiring .NET on the target. Build a framework-dependent version if you would rather have a small file and a .NET 8 runtime requirement.

**How do I change the interface language?**
A: The **Language** drop-down in the window, 中文 or English. The choice is written to `%LOCALAPPDATA%\WaveLinkWin10Setup\lang.cfg` and applies to the next launch and to elevated child processes. The default follows the OS display language: Chinese UI culture gives Chinese, everything else gives English.

**The exe reports "app install failed / Elgato.WaveLink not found".**
A: First confirm the MSIX in `input\` is a complete official package (around 186 MB, opens in a zip tool). On 1809 / 1909, go to section 2; the GUI installer has that route built in, so re-running it is enough. Upgrading to Windows 10 2004 or newer also gives you the unsigned path.

**Can the repository be completely free of binaries?**
A: Yes. Delete `driver/*.msi` and `driver/elgato/` and rely on `scripts\fetch_driver.bat` to download from the official CDN. The app MSIX was never in the repository to begin with.

## 9. What is left after the installer finishes

The scripts cover everything they can. Two things need a person:

1. Launch Wave Link, open `Settings → Output Routing`, and confirm all 17 endpoints are present and selectable.
2. Play some audio (route browser sound to a Wave output, for example) and confirm the meters move and you can hear it.

In the first two minutes after launch, `hr=0x88890004` may appear in the log. It is a Thesycon pipeline init race during pipeline rebuild; the devices are already `started` and the log also contains plenty of successful pipeline events. It settles on its own. If a specific route keeps producing that error during real playback and stays silent, the likely cause is an audio session or device held by another program, and that is where to look.