using System;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace WaveLinkWin10Setup
{
    /// <summary>
    /// Read-only environment check used by the "--check" CLI mode.
    /// Writes both to the console and to env_check.txt next to the exe (a WinExe
    /// has no console, so the file is the reliable artifact).
    /// </summary>
    public static class EnvironmentCheck
    {
        public static void Run()
        {
            var lines = new System.Collections.Generic.List<string>();
            void Emit(string s) { lines.Add(s); Console.WriteLine(s); }

            Emit("=== Wave Link Win10 Setup - 环境检查 (--check) ===");
            int build = Installer.GetOsBuild();
            Emit($"OS build            : {build} ({(build >= 17763 ? "满足 >= 1809" : "不满足，需 1809+")})");
            Emit($"Administrator       : {Installer.IsAdmin()}");

            bool dev = false;
            using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"))
                dev = k?.GetValue("AllowDevelopmentWithoutDevLicense") as int? == 1;
            Emit($"Developer Mode       : {dev}");

            string root = Installer.RepoRoot;
            Emit($"Repo root           : {root}");

            var inputDir = Path.Combine(root, "input");
            var msixs = Directory.Exists(inputDir) ? Directory.GetFiles(inputDir, "*.msix") : new string[0];
            Emit($"input/ MSIX         : {(msixs.Length > 0 ? string.Join(", ", msixs) : "无（需放入官方 MSIX）")}");

            var msi = Path.Combine(root, "driver", "WaveLinkDriver_3.0.0.466_x64.msi");
            Emit($"Driver MSI          : {(File.Exists(msi) ? "存在" : "缺失（将自动从 CDN 下载）")}");

            Emit("=== 检查结束 ===");

            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "env_check.txt");
                File.WriteAllLines(path, lines, Encoding.UTF8);
            }
            catch { /* non-fatal */ }
        }
    }
}
