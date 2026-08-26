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
            log("=== Wave Link 3.x on Windows 10 - 自动安装 ===");
            log("Repo root: " + RepoRoot);

            int build = GetOsBuild();
            log($"OS build: {build}");
            if (build != 0 && build < 17763)
                throw new Exception($"需要 Windows 10 1809 (build 17763) 或以上，当前 {build}。");

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
                    throw new Exception("未在 input/ 找到 MSIX。请把官方 Wave Link MSIX 放入 input/ 目录（见 input/README.txt）。");
                log("MSIX: " + msixPath);

                log("启用 Windows 开发者模式 ...");
                EnableDevMode(log);

                var patched = Path.Combine(RepoRoot, "input", "WaveLink_Win10_patched.msix");
                log($"补丁重打包（MinVersion -> 10.0.{minBuild}.0） ...");
                PatchMsix(msixPath, patched, minBuild, log);
                if (!File.Exists(patched)) throw new Exception("补丁未产生输出: " + patched);

                log("安装补丁后 MSIX（开发者模式免签名） ...");
                InstallAppx(patched, log);
            }
            else
            {
                log("(跳过应用安装)");
            }

            if (!skipDriver)
            {
                var msi = Path.Combine(RepoRoot, "driver", "WaveLinkDriver_3.0.0.466_x64.msi");
                if (!File.Exists(msi))
                {
                    log("驱动 MSI 缺失，尝试 fetch_driver.bat 下载 ...");
                    var bat = Path.Combine(RepoRoot, "scripts", "fetch_driver.bat");
                    if (File.Exists(bat)) RunProcess("cmd.exe", "/c \"" + bat + "\"", log);
                }
                if (!File.Exists(msi)) throw new Exception("驱动 MSI 缺失: " + msi);
                log("安装驱动 MSI（原生宿主 /qn） ...");
                InstallDriver(msi, log);
            }
            else
            {
                log("(跳过驱动安装)");
            }

            Verify(log);
            log("=== 完成。Wave Link 3.x 驱动已在 Windows 10 安装并运行。===");
        }

        public static void VerifyOnly(Action<string> log) => Verify(log);

        public static void EnvCheckGui(Action<string> log)
        {
            log("=== 环境检查 ===");
            int build = GetOsBuild();
            log($"OS build: {build} ({(build >= 17763 ? "满足 >= 1809" : "不满足，需 1809+")})");
            log("Administrator: " + IsAdmin());

            bool dev = false;
            using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"))
                dev = k?.GetValue("AllowDevelopmentWithoutDevLicense") as int? == 1;
            log("Developer Mode: " + dev);

            var inputDir = Path.Combine(RepoRoot, "input");
            var msixs = Directory.Exists(inputDir) ? Directory.GetFiles(inputDir, "*.msix") : new string[0];
            log("input/ MSIX: " + (msixs.Length > 0 ? string.Join(", ", msixs) : "无"));

            var msi = Path.Combine(RepoRoot, "driver", "WaveLinkDriver_3.0.0.466_x64.msi");
            log("Driver MSI: " + (File.Exists(msi) ? "存在" : "缺失"));
            log("=== 结束 ===");
        }

        // ---- internals ----

        static void EnableDevMode(Action<string> log)
        {
            using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
            key.SetValue("AllowDevelopmentWithoutDevLicense", 1, RegistryValueKind.DWord);
            log("开发者模式已启用。");
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
                throw new Exception("应用安装失败：未找到 Elgato.WaveLink（Windows 10 1809/1909 不支持 -AllowUnsigned，需用自签证书路线，见 FAQ）。");
            log("应用已安装: " + outp.Trim());
        }

        static void InstallDriver(string msi, Action<string> log)
        {
            var logPath = Path.Combine(RepoRoot, "driver", "msi_install_exe.log");
            var psi = new ProcessStartInfo("msiexec.exe", $"/i \"{msi}\" /qn /norestart /l*v \"{logPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi) ?? throw new Exception("无法启动 msiexec。");
            p.WaitForExit();
            if (p.ExitCode != 0) throw new Exception($"驱动 MSI 失败，退出码 {p.ExitCode}。详见 {logPath}");
            log("驱动 MSI 安装成功 (exit 0)。");
        }

        static void Verify(Action<string> log)
        {
            log("验证服务 ...");
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
            log("  Appx Elgato.WaveLink : " + (string.IsNullOrWhiteSpace(appx) ? "MISSING" : appx.Trim()));
            if (!ok) log("警告：部分服务未运行，请检查上方输出与日志。");
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
            using var p = Process.Start(psi) ?? throw new Exception("无法启动: " + exe);
            p.OutputDataReceived += (s, e) => { if (e.Data != null) log(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) log(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
            if (p.ExitCode != 0) throw new Exception($"{exe} 退出码 {p.ExitCode}");
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
