using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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

    /// <summary>`adb mdns services` 한 줄. 예: "adb-R58M...-AbCd  _adb-tls-connect._tcp  192.168.0.5:37011"</summary>
    public readonly struct MdnsService
    {
        public readonly string Name;
        public readonly string Type;
        public readonly string Address;

        public MdnsService(string name, string type, string address)
        {
            Name = name;
            Type = type;
            Address = address;
        }

        public bool IsPairing => Type.StartsWith("_adb-tls-pairing", StringComparison.Ordinal);
        public bool IsConnect => Type.StartsWith("_adb-tls-connect", StringComparison.Ordinal);
        public string Host => Address.Contains(":") ? Address.Substring(0, Address.LastIndexOf(':')) : Address;
    }

    public readonly struct AdbDevice
    {
        public readonly string Serial;
        public readonly string State;
        public readonly string Model;

        public AdbDevice(string serial, string state, string model)
        {
            Serial = serial;
            State = state;
            Model = model;
        }

        public bool IsWireless => Serial.Contains(":") || Serial.Contains("._adb-tls-connect.");
    }

    /// <summary>Unity가 쓰는 Android SDK의 adb를 비동기로 실행한다.</summary>
    public static class AdbClient
    {
        public const string AdbPathPrefKey = "AndroidWireless.AdbPathOverride";
        private const int DefaultTimeoutMs = 15000;

        private static readonly Regex PairedGuidRegex = new Regex(@"\[guid=([^\]]+)\]");

        public static string OverridePath
        {
            get => EditorPrefs.GetString(AdbPathPrefKey, "");
            set => EditorPrefs.SetString(AdbPathPrefKey, value ?? "");
        }

        /// <summary>
        /// 설정한 경로가 있으면 그걸 쓰고, 없으면 Unity Android 모듈의 SDK 안에 있는 adb를 쓴다.
        /// Unity와 다른 adb를 쓰면 서버 버전이 달라 서로 adb 서버를 재시작시키므로 기본값을 권장.
        /// </summary>
        public static string ResolveAdbPath()
        {
            string overridePath = OverridePath;
            if (!string.IsNullOrEmpty(overridePath)) return overridePath;

            string sdkRoot = GetUnityAndroidSdkRoot();
            if (string.IsNullOrEmpty(sdkRoot)) return null;

            string exe = Path.Combine(sdkRoot, "platform-tools", IsWindows ? "adb.exe" : "adb");
            return File.Exists(exe) ? exe : null;
        }

        private static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;

        // UnityEditor.Android는 Android 모듈이 설치된 경우에만 존재하므로 리플렉션으로 접근한다.
        private static string GetUnityAndroidSdkRoot()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("UnityEditor.Android.AndroidExternalToolsSettings");
                if (type == null) continue;
                var prop = type.GetProperty("sdkRootPath",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                return prop?.GetValue(null) as string;
            }
            return null;
        }

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

                    // 코드 인자 없이 pair가 실행되면 stdin을 기다리므로 바로 닫는다.
                    process.StandardInput.Close();
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();

                    var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                    while (!process.WaitForExit(100))
                    {
                        if (token.IsCancellationRequested || DateTime.UtcNow > deadline)
                        {
                            try { process.Kill(); } catch { /* 이미 종료됨 */ }
                            return new AdbResult(-1, token.IsCancellationRequested ? "취소됨" : $"시간 초과: adb {arguments}");
                        }
                    }

                    string output = (stdout.Result + stderr.Result).Trim();
                    return new AdbResult(process.ExitCode, output);
                }
            }, token);
        }

        public static async Task<string> GetVersionAsync()
        {
            var result = await RunAsync("version");
            if (!result.Success) return null;
            // "Version 36.0.0-13206524"
            var match = Regex.Match(result.Output, @"Version\s+(\S+)");
            return match.Success ? match.Groups[1].Value : result.Output.Split('\n')[0];
        }

        public static async Task<List<MdnsService>> GetMdnsServicesAsync(CancellationToken token)
        {
            var list = new List<MdnsService>();
            var result = await RunAsync("mdns services", token, 5000);
            if (!result.Success) return list;

            foreach (var raw in result.Output.Split('\n'))
            {
                var parts = raw.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3 || !parts[1].StartsWith("_adb", StringComparison.Ordinal)) continue;
                list.Add(new MdnsService(parts[0], parts[1], parts[2]));
            }
            return list;
        }

        /// <summary>성공 시 연결 서비스 이름(guid)을 돌려준다. 실패 시 null.</summary>
        public static async Task<(bool ok, string guid, string message)> PairAsync(string address, string code, CancellationToken token)
        {
            var result = await RunAsync($"pair {address} {code}", token, 20000);
            bool ok = result.Output.IndexOf("Successfully paired", StringComparison.OrdinalIgnoreCase) >= 0;
            var guid = PairedGuidRegex.Match(result.Output);
            return (ok, guid.Success ? guid.Groups[1].Value : null, result.Output);
        }

        public static async Task<(bool ok, string message)> ConnectAsync(string address, CancellationToken token)
        {
            var result = await RunAsync($"connect {address}", token, 15000);
            string output = result.Output;
            bool ok = output.IndexOf("connected to", StringComparison.OrdinalIgnoreCase) >= 0
                      && output.IndexOf("failed", StringComparison.OrdinalIgnoreCase) < 0
                      && output.IndexOf("cannot", StringComparison.OrdinalIgnoreCase) < 0;
            return (ok, output);
        }

        public static Task<AdbResult> DisconnectAsync(string serial) => RunAsync($"disconnect {serial}");

        public static async Task<List<AdbDevice>> GetDevicesAsync(CancellationToken token = default)
        {
            var list = new List<AdbDevice>();
            var result = await RunAsync("devices -l", token, 5000);
            if (!result.Success) return list;

            foreach (var raw in result.Output.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.Ordinal) || line.StartsWith("*", StringComparison.Ordinal))
                    continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                string model = "";
                foreach (var p in parts)
                    if (p.StartsWith("model:", StringComparison.Ordinal)) model = p.Substring(6).Replace('_', ' ');

                list.Add(new AdbDevice(parts[0], parts[1], model));
            }
            return list;
        }
    }
}
