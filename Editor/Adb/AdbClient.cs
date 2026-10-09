using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace AndroidWireless
{
    public readonly struct AdbResult
    {
        public readonly int ExitCode;
        public readonly string Output;

        public AdbResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output;
        }

        public bool Success => ExitCode == 0;
    }

    /// <summary>
    /// adb 실행 파일을 찾아 실행한다. 기기 조회·페어링·연결은 <see cref="AdbServer"/>가 서버 소켓으로 처리하고,
    /// 이 클래스는 서버가 떠 있지 않을 때 `adb start-server`를 실행하는 용도로만 쓴다.
    /// (다른 버전의 adb CLI를 실행하면 이미 떠 있는 서버가 재시작되므로 최소한으로 사용)
    /// </summary>
    public static class AdbClient
    {
        public const string AdbPathPrefKey = "AndroidWireless.AdbPathOverride";
        private const int DefaultTimeoutMs = 15000;
        private const int OutputDrainMs = 1000;

        public static string OverridePath
        {
            get => EditorPrefs.GetString(AdbPathPrefKey, "");
            set => EditorPrefs.SetString(AdbPathPrefKey, value ?? "");
        }

        /// <summary>
        /// 설정한 경로가 있으면 그걸 쓰고, 없으면 Unity Android SDK·ANDROID_HOME·기본 SDK 위치의 adb 중 가장 최신 버전을 쓴다.
        /// (낮은 버전의 adb로 서버를 띄우면 Android Studio 등 최신 adb가 서버를 다시 죽이고 띄우는 경쟁이 생긴다)
        /// </summary>
        public static string ResolveAdbPath()
        {
            string overridePath = OverridePath;
            if (!string.IsNullOrEmpty(overridePath)) return overridePath;

            string best = null;
            Version bestVersion = null;
            foreach (var sdkRoot in CandidateSdkRoots())
            {
                if (string.IsNullOrEmpty(sdkRoot)) continue;
                string tools = Path.Combine(sdkRoot, "platform-tools");
                string exe = Path.Combine(tools, IsWindows ? "adb.exe" : "adb");
                if (!File.Exists(exe)) continue;

                var version = AndroidSdk.ReadPlatformToolsVersion(sdkRoot);
                if (best == null || (version != null && (bestVersion == null || version > bestVersion)))
                {
                    best = exe;
                    bestVersion = version;
                }
            }
            return best;
        }

        private static IEnumerable<string> CandidateSdkRoots()
        {
            yield return AndroidSdk.SdkRoot;
            yield return Environment.GetEnvironmentVariable("ANDROID_HOME");
            yield return Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return IsWindows
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk")
                : Path.Combine(home, "Library", "Android", "sdk");
        }


        private static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;


        public static Task<AdbResult> RunAsync(string arguments, CancellationToken token = default, int timeoutMs = DefaultTimeoutMs)
        {
            string adb = ResolveAdbPath();
            if (string.IsNullOrEmpty(adb))
                return Task.FromResult(new AdbResult(-1, "adb를 찾을 수 없습니다. Android 모듈 설치 또는 adb 경로를 확인하세요."));

            return Task.Run(() =>
            {
                var psi = new ProcessStartInfo(adb, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                };

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

                    process.StandardInput.Close();
                    var output = new StringBuilder();
                    var stdoutClosed = new ManualResetEventSlim();
                    var stderrClosed = new ManualResetEventSlim();
                    process.OutputDataReceived += (_, e) => { if (e.Data == null) stdoutClosed.Set(); else lock (output) output.AppendLine(e.Data); };
                    process.ErrorDataReceived += (_, e) => { if (e.Data == null) stderrClosed.Set(); else lock (output) output.AppendLine(e.Data); };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                    while (!process.WaitForExit(100))
                    {
                        if (token.IsCancellationRequested || DateTime.UtcNow > deadline)
                        {
                            try { process.Kill(); } catch { /* 이미 종료됨 */ }
                            return new AdbResult(-1, token.IsCancellationRequested ? "취소됨" : $"시간 초과: adb {arguments}");
                        }
                    }

                    // start-server가 띄운 데몬이 파이프 핸들을 물려받으면(구버전 adb, 특히 Windows) EOF가 오지 않는다.
                    // adb는 이미 종료했으므로 출력은 잠깐만 기다리고 받은 데까지만 쓴다.
                    stdoutClosed.Wait(OutputDrainMs);
                    stderrClosed.Wait(OutputDrainMs);

                    string text;
                    lock (output) text = output.ToString();
                    return new AdbResult(process.ExitCode, text.Trim());
                }
            }, token);
        }
    }
}
