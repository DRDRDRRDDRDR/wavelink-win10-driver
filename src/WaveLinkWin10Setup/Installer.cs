using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;

namespace WaveLinkWin10Setup
{
    /// <summary>
    /// Core orchestration for the Wave Link 3.x on Windows 10 setup.
    /// GUI-agnostic: reports progress through an Action&lt;string&gt; log callback.
    /// </summary>
    public static class Installer
    {
        /// <summary>Folder the exe lives in; driver/ and input/ are expected next to it.</summary>
        public static string RepoRoot => AppContext.BaseDirectory.TrimEnd('\\');

        public static bool IsAdmin()
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static int GetOsBuild()
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var v = key?.GetValue("CurrentBuild")?.ToString();
            return v != null && int.TryParse(v, out var b) ? b : 0;
        }

        /// <summary>
        /// Full flow. mode: "all" | "app" | "driver". skipApp/skipDriver let callers narrow scope.
        /// </summary>
        public static void Run(string mode, string msixPath, int minBuild, bool skipApp, bool skipDriver, Action<string> log)
        {
            log(Lang.T("runHeader"));
            log(Lang.T("repoRoot") + RepoRoot);

            int build = GetOsBuild();
            log(string.Format(Lang.T("osBuild"), build));
            if (build != 0 && build < 17763)
                throw new Exception(string.Format(Lang.T("needBuild"), build));

            if (mode == "driver") skipApp = true;
            if (mode == "app") skipDriver = true;

            if (!skipApp)
            {
                if (string.IsNullOrWhiteSpace(msixPath))
                {
                    var inputDir = Path.Combine(RepoRoot, "input");
                    if (Directory.Exists(inputDir))
                    {
                        var c = Directory.GetFiles(inputDir, "*.msix");
                        if (c.Length > 0) msixPath = c[0];
                    }
                }
                if (string.IsNullOrWhiteSpace(msixPath) || !File.Exists(msixPath))
                    throw new Exception(Lang.T("noMsix"));
                log(Lang.T("msixPath") + msixPath);

                log(Lang.T("enableDev"));
                EnableDevMode(log);

                var patched = Path.Combine(RepoRoot, "input", "WaveLink_Win10_patched.msix");
                log(string.Format(Lang.T("patchRepack"), minBuild));
                PatchMsix(msixPath, patched, minBuild, log);
                if (!File.Exists(patched)) throw new Exception(Lang.T("patchNoOut") + patched);

                log(Lang.T("installPatched"));
                InstallAppx(patched, log);
            }
            else
            {
                log(Lang.T("skipApp"));
            }

            if (!skipDriver)
            {
                var msi = Path.Combine(RepoRoot, "driver", "WaveLinkDriver_3.0.0.466_x64.msi");
                if (!File.Exists(msi))
                {
                    log(Lang.T("driverMissingFetch"));
                    var bat = Path.Combine(RepoRoot, "scripts", "fetch_driver.bat");
                    if (File.Exists(bat)) RunProcess("cmd.exe", "/c \"" + bat + "\"", log);
                }
                if (!File.Exists(msi)) throw new Exception(Lang.T("driverMissing") + msi);
                log(Lang.T("installDriverMsi"));
                InstallDriver(msi, log);
            }
            else
            {
                log(Lang.T("skipDriver"));
            }

            Verify(log);
            log(Lang.T("done"));
        }

        public static void VerifyOnly(Action<string> log) => Verify(log);

        public static void EnvCheckGui(Action<string> log)
        {
            log(Lang.T("envHeader"));
            int build = GetOsBuild();
            log(string.Format(Lang.T("envBuildLine"), build, build >= 17763 ? Lang.T("envBuildOk") : Lang.T("envBuildBad")));
            log(Lang.T("envAdmin") + IsAdmin());

            bool dev = false;
            using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"))
                dev = k?.GetValue("AllowDevelopmentWithoutDevLicense") as int? == 1;
            log(Lang.T("envDev") + dev);

            var inputDir = Path.Combine(RepoRoot, "input");
            var msixs = Directory.Exists(inputDir) ? Directory.GetFiles(inputDir, "*.msix") : new string[0];
            log(Lang.T("envInputMsix") + (msixs.Length > 0 ? string.Join(", ", msixs) : Lang.T("inputNone")));

            var msi = Path.Combine(RepoRoot, "driver", "WaveLinkDriver_3.0.0.466_x64.msi");
            log(Lang.T("envDriverMsi") + (File.Exists(msi) ? Lang.T("driverPresent") : Lang.T("driverAbsent")));
            log(Lang.T("envEnd"));
        }

        // ---- internals ----

        static void EnableDevMode(Action<string> log)
        {
            using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
            key.SetValue("AllowDevelopmentWithoutDevLicense", 1, RegistryValueKind.DWord);
            log(Lang.T("devEnabled"));
        }

        static void PatchMsix(string input, string output, int minBuild, Action<string> log)
        {
            var ps1 = ExtractEmbeddedPs1();
            var tmp = Path.Combine(Path.GetTempPath(), "wl_patch_" + Guid.NewGuid().ToString("N") + ".ps1");
            File.WriteAllText(tmp, ps1, Encoding.UTF8);
            RunProcess("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{tmp}\" -InputMsix \"{input}\" -OutputMsix \"{output}\" -MinBuild {minBuild}",
                log);
        }

        static string ExtractEmbeddedPs1()
        {
            var asm = typeof(Installer).Assembly;
            using var s = asm.GetManifestResourceStream("WaveLinkWin10Setup.Resources.patch_manifest.ps1")
                ?? throw new Exception("内嵌补丁脚本缺失（patch_manifest.ps1）。");
            using var r = new StreamReader(s, Encoding.UTF8);
            return r.ReadToEnd();
        }

        static void InstallAppx(string patched, Action<string> log)
        {
            var cmd = $"Add-AppxPackage -Path '{patched.Replace("'", "''")}' -AllowUnsigned -ForceApplicationShutdown";
            RunProcess("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{cmd}\"", log);

            var ps = "Get-AppxPackage -Name Elgato.WaveLink | Select-Object -ExpandProperty Version";
            var outp = RunProcessCapture("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{ps}\"");
            if (string.IsNullOrWhiteSpace(outp))
                throw new Exception(Lang.T("appFail"));
            log(Lang.T("appInstalled") + outp.Trim());
        }

        static void InstallDriver(string msi, Action<string> log)
        {
            var logPath = Path.Combine(RepoRoot, "driver", "msi_install_exe.log");
            var psi = new ProcessStartInfo("msiexec.exe", $"/i \"{msi}\" /qn /norestart /l*v \"{logPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi) ?? throw new Exception(Lang.T("cannotStartMsiexec"));
            p.WaitForExit();
            if (p.ExitCode != 0) throw new Exception(string.Format(Lang.T("driverFail"), p.ExitCode, logPath));
            log(Lang.T("driverOk"));
        }

        static void Verify(Action<string> log)
        {
            log(Lang.T("verifyHeader"));
            bool ok = true;
            foreach (var name in new[] { "ElgatoVirtUsbAudioEmu", "ElgatoUsbAudio", "ElgatoUsbAudioks" })
            {
                var svc = ServiceController.GetServices().FirstOrDefault(x => x.ServiceName == name);
                var st = svc?.Status.ToString() ?? "MISSING";
                log($"  {name} : {st}");
                if (svc == null || svc.Status != ServiceControllerStatus.Running) ok = false;
            }
            var appx = RunProcessCapture("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage -Name Elgato.WaveLink | Select-Object -ExpandProperty Version\"");
            log(Lang.T("verifyAppx") + (string.IsNullOrWhiteSpace(appx) ? "MISSING" : appx.Trim()));
            if (!ok) log(Lang.T("verifyWarn"));
        }

        static void RunProcess(string exe, string args, Action<string> log)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using var p = Process.Start(psi) ?? throw new Exception(Lang.T("cannotStart") + exe);
            p.OutputDataReceived += (s, e) => { if (e.Data != null) log(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) log(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
            if (p.ExitCode != 0) throw new Exception(string.Format(Lang.T("exitCode"), exe, p.ExitCode));
        }

        static string RunProcessCapture(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using var p = Process.Start(psi) ?? throw new Exception("无法启动: " + exe);
            var o = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return o;
        }
    }
}
