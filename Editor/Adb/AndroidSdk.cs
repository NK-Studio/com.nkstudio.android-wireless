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

        /// <summary>
        /// Unity SDK에 최신 platform-tools를 설치한다. 호출 전에 사용자에게 Android SDK 라이선스 동의를 받아야 한다
        /// (sdkmanager의 라이선스 질문에 "y"로 답한다).
        /// </summary>
        public static Task<AdbResult> InstallPlatformToolsAsync(CancellationToken token)
        {
            string sdkManager = FindSdkManager();
            string sdkRoot = SdkRoot;
            string jdkRoot = JdkRoot;
            if (sdkManager == null)
                return Task.FromResult(new AdbResult(-1, Localization.Get("update.noSdkManager")));

            return Task.Run(() =>
            {
                var psi = new ProcessStartInfo(sdkManager, $"--sdk_root=\"{sdkRoot}\" \"platform-tools\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                };
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
                            try { process.Kill(); } catch { /* 이미 종료됨 */ }
                            return new AdbResult(-1, token.IsCancellationRequested ? "Cancelled" : "Timed out");
                        }
                    }
                    process.WaitForExit();

                    string text;
                    lock (output) text = output.ToString();
                    return new AdbResult(process.ExitCode, text.Trim());
                }
            }, token);
        }
    }
}
