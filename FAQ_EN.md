# FAQ & Manual Principles (English)

> This page collects: how the MSIX Win11 gate is bypassed, the manual re-sign route, manual driver install, and the FAQ.
> One-click users do not need to read this — see [README_EN.md](./README_EN.md).

## 1. How the MSIX Win11 gate is bypassed (for understanding)

The MSIX's `AppxManifest.xml` declares `MinVersion="10.0.22000.0"` (22000 = first Win11 build), so the Windows 10 AppX installer refuses it. To install, four things must happen:

1. **Unpack the MSIX** (makeappx unpack, or any zip tool) → get `AppxManifest.xml`.
2. **Edit the manifest**: lower `MinVersion="10.0.22000.0"` to `10.0.19041.0` (Win10 2004), and change the `<Identity>` `Publisher` to a self-signed subject such as `CN=WaveLinkPatch`.
3. **Delete the old `AppxSignature.p7x`** (the signature is invalid once the manifest changes).
4. **Repack + install**: under **Developer Mode**, install unsigned with `Add-AppxPackage -AllowUnsigned` (no Elgato private key required — most universal).

`scripts/patch_manifest.ps1` automates the unpack → edit → repack part; `scripts/setup_wavelink_win10.ps1` additionally enables Developer Mode and installs on top of that.

## 2. Manual re-sign route (only for 1809 / 1909, or if you prefer certs)

If your OS lacks `-AllowUnsigned` (e.g. 1809/1909), re-sign the repacked MSIX with a trusted certificate:

```bat
:: 1) Patch (produces input\WaveLink_Win10_patched.msix)
PowerShell -ExecutionPolicy Bypass -File scripts\patch_manifest.ps1 -InputMsix input\your.msix

:: 2) Re-sign with your .pfx (needs Windows SDK signtool)
signtool sign /fd SHA256 /a /f your-cert.pfx /p password input\WaveLink_Win10_patched.msix

:: 3) Import cert to Trusted Root (install.ps1 shows the pattern), then install
Add-AppxPackage input\WaveLink_Win10_patched.msix
```

> Note: this repo ships **no .pfx private key**; `certs/WaveLinkPatch.cer` is only the public half of the cert used during the original repack and cannot re-sign MSIXs for others.

## 3. Manual driver install (copy-paste)

If you prefer to install the driver manually instead of using `reinstall_wavelink_driver.bat`:

1. Open Command Prompt (CMD) **as administrator**: press `Win`, type `cmd`, right-click → Run as administrator.
2. Run line by line (adjust the path to where you extracted):
```bat
cd /d "C:\wavelink-win10-driver\driver"
msiexec /i WaveLinkDriver_3.0.0.466_x64.msi /qn /norestart /l*v msi_install.log
```
3. The tail of `msi_install.log` should show `Return Value 0`.

> ⚠️ **Critical gotcha**: Always launch `msiexec` from a native Windows host (cmd.exe / PowerShell). Under **Git Bash / MSYS** the path `C:\Users\...` is rewritten to `/Users\...`, causing server error `Note 1314` and exit code **83**. Use CMD or `scripts/install.ps1`.

## 4. FAQ

**Q1: The one-click script flashes by / does nothing?**
A: Run it manually from an elevated PowerShell at the repo root: `PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1` to see full output. Some AV may block Developer Mode / AppX install — allow temporarily.

**Q2: `Add-AppxPackage` says "-AllowUnsigned is not supported"?**
A: That parameter requires Windows 10 **2004 (19041) or newer** Developer Mode. On 1809/1909 use "2. Manual re-sign route" (sign with a trusted .pfx, then install).

**Q3: msiexec exit code 83?**
A: Almost always the Git Bash/MSYS path translation. Use native CMD or `install.ps1`, keeping the path as `C:\...`. See the warning in "3. Manual driver install".

**Q4: Log shows `Found 0 drivers` — did it fail?**
A: No. That is the runtime querying the server for updates (Win10 server delivers none), not a local absence. The same log shows `Found current driver version: 3.0.0.466`, proving the local driver is present.

**Q5: Can the repo be completely binary-free?**
A: Yes. Delete `driver/*.msi` and use `scripts/fetch_driver.bat`; the app MSIX is never in the repo anyway (in `input/`, ignored by `.gitignore`).

**Q6: I'm on Windows 10 1809 / 1909 / 21H1 — will it work?**
A: The driver install layer is **theoretically supported on 1809 (17763) and above** (VirtUsbAudioEmu can go to 1803). The app auto-install path needs 2004+ (due to `-AllowUnsigned`); on 1809/1909 see "2. Manual re-sign route". Only 22H2 is empirically verified — try it and report back.
