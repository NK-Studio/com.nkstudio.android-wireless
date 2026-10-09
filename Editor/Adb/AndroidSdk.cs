using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace AndroidWireless
{
    /// <summary>Unity Android 모듈과 그 안의 Android SDK(platform-tools) 상태.</summary>
    public static class AndroidSdk
    {
        /// <summary>ADB Wi-Fi 2.0에 필요한 Platform-Tools 메이저 버전.</summary>
        public const int RequiredPlatformToolsMajor = 37;

        private const int InstallTimeoutMs = 10 * 60 * 1000;

        /// <summary>
        /// android-sdk-license(2019-01-16판) 동의 기록. `sdkmanager --licenses`가 licenses/android-sdk-license에 남기는 값과 같다.
        /// Windows의 sdkmanager는 리다이렉트된 stdin의 "y"를 읽지 않으므로 동의를 이 파일로 전달한다.
        /// </summary>
        private const string SdkLicenseHash = "24333f8a63b6825ea9c5514f83c2829b004d1fee";

        // UnityEditor.Android는 Android 모듈이 설치된 경우에만 존재하므로 리플렉션으로 접근한다.
        private static Type ToolsSettingsType =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.Android.AndroidExternalToolsSettings"))
                .FirstOrDefault(t => t != null);

        public static bool IsModuleInstalled =>
            ToolsSettingsType != null && BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);

        public static string SdkRoot => GetToolsPath("sdkRootPath");
        public static string JdkRoot => GetToolsPath("jdkRootPath");

        private static string GetToolsPath(string property)
        {
            var prop = ToolsSettingsType?.GetProperty(property, BindingFlags.Static | BindingFlags.Public);
            return prop?.GetValue(null) as string;
        }

        /// <summary>Unity가 쓰는 SDK의 platform-tools 버전. 알 수 없으면 null.</summary>
        public static Version PlatformToolsVersion => ReadPlatformToolsVersion(SdkRoot);

        public static bool NeedsPlatformToolsUpdate
        {
            get
            {
                var version = PlatformToolsVersion;
                return version != null && version.Major < RequiredPlatformToolsMajor;
            }
        }

        /// <summary>platform-tools/source.properties의 Pkg.Revision (예: 37.0.1).</summary>
        public static Version ReadPlatformToolsVersion(string sdkRoot)
        {
            if (string.IsNullOrEmpty(sdkRoot)) return null;
            try
            {
                string file = Path.Combine(sdkRoot, "platform-tools", "source.properties");
                if (!File.Exists(file)) return null;
                foreach (var line in File.ReadAllLines(file))
                {
                    if (!line.StartsWith("Pkg.Revision", StringComparison.Ordinal)) continue;
                    string value = line.Substring(line.IndexOf('=') + 1).Trim();
                    int cut = value.IndexOfAny(new[] { '-', ' ' });
                    if (cut > 0) value = value.Substring(0, cut);
                    return Version.TryParse(value, out var v) ? v : null;
                }
            }
            catch (IOException) { }
            return null;
        }

        /// <summary>SDK의 cmdline-tools/{latest|버전}/bin/sdkmanager. 가장 높은 버전을 고른다.</summary>
        public static string FindSdkManager()
        {
            string root = SdkRoot;
            if (string.IsNullOrEmpty(root)) return null;
            string dir = Path.Combine(root, "cmdline-tools");
            if (!Directory.Exists(dir)) return null;

            string exe = Environment.OSVersion.Platform == PlatformID.Win32NT ? "sdkmanager.bat" : "sdkmanager";
            var candidates = new List<(Version version, string path)>();
            foreach (var sub in Directory.GetDirectories(dir))
            {
                string path = Path.Combine(sub, "bin", exe);
                if (!File.Exists(path)) continue;
                string name = Path.GetFileName(sub);
                var version = name == "latest" ? new Version(int.MaxValue, 0) : (Version.TryParse(name, out var v) ? v : new Version(0, 0));
                candidates.Add((version, path));
            }
            return candidates.OrderByDescending(c => c.version).Select(c => c.path).FirstOrDefault();
        }

        /// <summary>설치 중에는 adb 서버를 자동으로 다시 띄우지 않는다 (Windows에서 adb.exe가 잠겨 설치가 실패함).</summary>
        public static bool IsInstallingPlatformTools => installing;
        private static volatile bool installing;

        private static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;

        /// <summary>
        /// Unity SDK에 최신 platform-tools를 설치한다. 호출 전에 사용자에게 Android SDK 라이선스 동의를 받아야 한다
        /// (sdkmanager의 라이선스 질문에 "y"로 답한다).
        /// </summary>
        public static async Task<AdbResult> InstallPlatformToolsAsync(CancellationToken token)
        {
            string sdkManager = FindSdkManager();
            string sdkRoot = SdkRoot;
            string jdkRoot = JdkRoot;
            if (sdkManager == null)
                return new AdbResult(-1, Localization.Get("update.noSdkManager"));

            installing = true;
            try
            {
                // Windows는 실행 중인 adb.exe를 덮어쓸 수 없으므로 서버를 먼저 내린다.
                // 설치가 끝나면 WirelessDiscovery가 새 adb로 서버를 다시 띄운다.
                if (IsWindows) await AdbServer.KillAsync(token);

                // Unity가 Program Files에 설치돼 있으면 SDK 폴더에 쓸 수 없다 → 관리자 권한(UAC)으로 실행한다.
                bool elevate = IsWindows && !CanWrite(sdkRoot);
                return await Task.Run(() => elevate
                    ? RunSdkManagerElevated(sdkManager, sdkRoot, jdkRoot, token)
                    : RunSdkManager(sdkManager, sdkRoot, jdkRoot, token), token);
            }
            catch (OperationCanceledException)
            {
                return new AdbResult(-1, "Cancelled");
            }
            finally
            {
                installing = false;
            }
        }

        /// <summary>라이선스 동의를 licenses/android-sdk-license에 기록한다 (사용자가 확인창에서 동의한 뒤에만 호출).</summary>
        private static void AcceptSdkLicense(string sdkRoot)
        {
            try
            {
                string dir = Path.Combine(sdkRoot, "licenses");
                string file = Path.Combine(dir, "android-sdk-license");
                Directory.CreateDirectory(dir);
                if (File.Exists(file) && File.ReadAllText(file).Contains(SdkLicenseHash)) return;
                File.AppendAllText(file, "\n" + SdkLicenseHash);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 기록하지 못하면 sdkmanager의 라이선스 질문(stdin "y")에 맡긴다.
            }
        }

        private static AdbResult RunSdkManager(string sdkManager, string sdkRoot, string jdkRoot, CancellationToken token)
        {
            AcceptSdkLicense(sdkRoot);

            // 공백이 있는 경로의 .bat을 바로 실행하면 cmd의 따옴표 처리가 깨지므로 cmd /s /c로 감싼다.
            string args = $"--sdk_root=\"{sdkRoot}\" \"platform-tools\"";
            var psi = IsWindows
                ? new ProcessStartInfo("cmd.exe", $"/d /s /c \"\"{sdkManager}\" {args}\"")
                : new ProcessStartInfo(sdkManager, args);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = true;
            psi.CreateNoWindow = true;
            if (!string.IsNullOrEmpty(jdkRoot)) psi.EnvironmentVariables["JAVA_HOME"] = jdkRoot;

            using (var process = new Process { StartInfo = psi })
            {
                try
                {
                    process.Start();
                }
                catch (Exception e)
                {
                    return new AdbResult(-1, e.Message);
                }

                var output = new StringBuilder();
                process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 라이선스 질문에 대한 답 (사용자가 확인창에서 동의한 뒤에만 이 메서드를 호출한다)
                try
                {
                    for (int i = 0; i < 10; i++) process.StandardInput.WriteLine("y");
                    process.StandardInput.Close();
                }
                catch (IOException) { }

                var deadline = DateTime.UtcNow.AddMilliseconds(InstallTimeoutMs);
                while (!process.WaitForExit(200))
                {
                    if (token.IsCancellationRequested || DateTime.UtcNow > deadline)
                    {
                        KillTree(process);
                        return new AdbResult(-1, token.IsCancellationRequested ? "Cancelled" : "Timed out");
                    }
                }
                process.WaitForExit();

                string text;
                lock (output) text = output.ToString();
                return new AdbResult(process.ExitCode, text.Trim());
            }
        }

        /// <summary>
        /// Windows 전용. 임시 .cmd 스크립트를 관리자 권한으로 실행한다. 권한 상승된 프로세스의 출력은
        /// 리다이렉트할 수 없으므로 로그 파일로 받는다.
        /// </summary>
        private static AdbResult RunSdkManagerElevated(string sdkManager, string sdkRoot, string jdkRoot, CancellationToken token)
        {
            string dir = Path.Combine(Path.GetTempPath(), "AndroidWireless-" + Guid.NewGuid().ToString("N"));
            string script = Path.Combine(dir, "install-platform-tools.cmd");
            string log = Path.Combine(dir, "install.log");
            try
            {
                Directory.CreateDirectory(dir);
                var lines = new StringBuilder();
                lines.AppendLine("@echo off");
                lines.AppendLine("chcp 65001 >nul"); // 경로에 한글 등이 있어도 UTF-8로 읽도록
                if (!string.IsNullOrEmpty(jdkRoot)) lines.AppendLine($"set \"JAVA_HOME={jdkRoot}\"");
                // 라이선스 동의 기록 (사용자가 확인창에서 동의한 뒤에만 실행한다). sdkmanager는 stdin 응답을 읽지 않는다.
                string licenses = Path.Combine(sdkRoot, "licenses");
                string license = Path.Combine(licenses, "android-sdk-license");
                lines.AppendLine($"if not exist \"{licenses}\" mkdir \"{licenses}\"");
                lines.AppendLine($"findstr /c:\"{SdkLicenseHash}\" \"{license}\" >nul 2>&1 || (>>\"{license}\" echo.& >>\"{license}\" echo {SdkLicenseHash})");
                lines.AppendLine($"call \"{sdkManager}\" --sdk_root=\"{sdkRoot}\" \"platform-tools\" < nul > \"{log}\" 2>&1");
                lines.AppendLine("exit /b %errorlevel%");
                File.WriteAllText(script, lines.ToString(), new UTF8Encoding(false));

                var psi = new ProcessStartInfo(script)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                Process process;
                try
                {
                    process = Process.Start(psi);
                }
                catch (System.ComponentModel.Win32Exception e) when (e.NativeErrorCode == 1223) // ERROR_CANCELLED
                {
                    return new AdbResult(-1, Localization.Get("update.elevationDenied"));
                }
                catch (Exception e)
                {
                    return new AdbResult(-1, e.Message);
                }
                if (process == null) return new AdbResult(-1, "Failed to start sdkmanager");

                using (process)
                {
                    // 권한 상승된 프로세스는 이 에디터에서 종료할 수 없으므로 취소 시 기다리기만 멈춘다.
                    var deadline = DateTime.UtcNow.AddMilliseconds(InstallTimeoutMs);
                    while (!process.WaitForExit(200))
                    {
                        if (token.IsCancellationRequested) return new AdbResult(-1, "Cancelled");
                        if (DateTime.UtcNow > deadline) return new AdbResult(-1, "Timed out");
                    }

                    string text = File.Exists(log) ? File.ReadAllText(log) : "";
                    return new AdbResult(process.ExitCode, text.Trim());
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return new AdbResult(-1, e.Message);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { /* 정리 실패는 무시 */ }
            }
        }

        private static bool CanWrite(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return true; // 판단 불가 → sdkmanager 오류에 맡긴다
            try
            {
                string probe = Path.Combine(directory, ".aw-write-test-" + Guid.NewGuid().ToString("N"));
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
            {
                return false;
            }
        }

        /// <summary>Windows에서 .bat만 종료하면 자식 java가 남으므로 프로세스 트리를 통째로 종료한다.</summary>
        private static void KillTree(Process process)
        {
            try
            {
                if (IsWindows)
                {
                    var psi = new ProcessStartInfo("taskkill", $"/T /F /PID {process.Id}")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    using (var kill = Process.Start(psi))
                        kill?.WaitForExit(5000);
                }
                if (!process.HasExited) process.Kill();
            }
            catch { /* 이미 종료됨 */ }
        }
    }
}
