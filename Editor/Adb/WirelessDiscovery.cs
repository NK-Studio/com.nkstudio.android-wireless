using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AndroidWireless
{
    /// <summary>
    /// adb 서버의 mDNS 서비스와 연결된 기기 목록을 스트림으로 구독해 기기 목록을 실시간으로 유지한다.
    /// - `host:track-mdns-services`: 기기 이름·API·ADB Wi-Fi 버전까지 오는 protobuf 스트림 (platform-tools 37+, 36.0.0은 미지원)
    /// - `host:mdns:services`: 구버전 서버용 텍스트 폴백 (2초 폴링, 이름·주소만)
    /// - `host:track-devices`: 이미 연결된 기기 표시용
    /// 모든 콜백은 에디터 메인 스레드(UnitySynchronizationContext)에서 실행된다.
    /// </summary>
    public sealed class WirelessDiscovery : IDisposable
    {
        private const int RetryDelayMs = 1500;
        private const int PollIntervalMs = 2000;
        private const int FallbackRetryMs = 10000;

        public event Action Changed;

        private CancellationTokenSource cts;
        private List<MdnsServiceInfo> services = new List<MdnsServiceInfo>();
        private Dictionary<string, string> adbDevices = new Dictionary<string, string>();
        private List<WirelessDevice> devices = new List<WirelessDevice>();
        private Task<string> ensureServerTask;
        private DateTime unreachableSince = DateTime.MaxValue;

        /// <summary>
        /// 서버가 이만큼 계속 응답하지 않을 때만 직접 띄운다. 다른 도구(Android Studio, Unity 빌드)가
        /// 서버를 재시작하는 짧은 틈에 끼어들면 서로 서버를 죽이고 띄우는 경쟁이 생긴다.
        /// </summary>
        private static readonly TimeSpan StartServerAfter = TimeSpan.FromSeconds(6);

        public IReadOnlyList<MdnsServiceInfo> Services => services;
        public IReadOnlyList<WirelessDevice> Devices => devices;

        /// <summary>serial → state ("device", "offline", "unauthorized" …)</summary>
        public IReadOnlyDictionary<string, string> AdbDevices => adbDevices;

        /// <summary>false면 구버전 adb 서버라 모델·API·ADB Wi-Fi 버전을 알 수 없다.</summary>
        public bool HasDetailedInfo { get; private set; } = true;

        /// <summary>첫 응답을 받기 전까지 true.</summary>
        public bool IsStarting { get; private set; } = true;

        public string Error { get; private set; }

        public void Start()
        {
            if (cts != null) return;
            cts = new CancellationTokenSource();
            _ = RunMdnsLoopAsync(cts.Token);
            _ = RunDeviceLoopAsync(cts.Token);
        }

        public void Dispose()
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
            Changed = null;
        }

        /// <summary>probe가 null이 아닌 값을 돌려줄 때까지 기다린다.</summary>
        public Task<T> WaitForAsync<T>(Func<WirelessDiscovery, T> probe, CancellationToken token) where T : class
        {
            var hit = probe(this);
            if (hit != null) return Task.FromResult(hit);

            var tcs = new TaskCompletionSource<T>();
            CancellationTokenRegistration registration = default;
            Action handler = null;
            handler = () =>
            {
                var result = probe(this);
                if (result == null) return;
                Changed -= handler;
                registration.Dispose();
                tcs.TrySetResult(result);
            };
            Changed += handler;
            registration = token.Register(() =>
            {
                Changed -= handler;
                tcs.TrySetCanceled(token);
            });
            return tcs.Task;
        }

        public WirelessDevice FindDevice(string key) => devices.FirstOrDefault(d => d.Key == key);

        // ---------------------------------------------------------------- mDNS

        private async Task RunMdnsLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var connection = await OpenAsync("host:track-mdns-services", token))
                    {
                        HasDetailedInfo = true;
                        while (true)
                        {
                            byte[] frame = await connection.ReadFrameAsync();
                            if (frame == null) break;
                            SetServices(MdnsServiceInfo.ParseServices(frame), null);
                        }
                    }
                    SetServices(new List<MdnsServiceInfo>(), Localization.Get("error.lostServer"));
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (AdbFailException)
                {
                    // 구버전 서버는 track-mdns-services를 모른다 → 잠시 텍스트 폴링 후 다시 시도한다.
                    // (다른 도구가 서버를 최신 adb로 다시 띄우면 상세 정보 스트림으로 돌아온다)
                    HasDetailedInfo = false;
                    await PollMdnsTextAsync(token, FallbackRetryMs);
                    continue;
                }
                catch (Exception e)
                {
                    SetServices(new List<MdnsServiceInfo>(), Describe(e));
                }

                if (!await DelayAsync(RetryDelayMs, token)) return;
            }
        }

        private async Task PollMdnsTextAsync(CancellationToken token, int durationMs)
        {
            var until = DateTime.UtcNow.AddMilliseconds(durationMs);
            while (!token.IsCancellationRequested && DateTime.UtcNow < until)
            {
                try
                {
                    string text;
                    using (var connection = await OpenAsync("host:mdns:services", token))
                        text = await connection.ReadStringAsync() ?? "";
                    SetServices(MdnsServiceInfo.ParseText(text), null);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    SetServices(new List<MdnsServiceInfo>(), Describe(e));
                }

                if (!await DelayAsync(PollIntervalMs, token)) return;
            }
        }

        private void SetServices(List<MdnsServiceInfo> list, string error)
        {
            services = list;
            Error = error;
            IsStarting = false;
            Rebuild();
        }

        // ---------------------------------------------------------------- adb devices

        private async Task RunDeviceLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var connection = await OpenAsync("host:track-devices", token))
                    {
                        while (true)
                        {
                            byte[] frame = await connection.ReadFrameAsync();
                            if (frame == null) break;
                            adbDevices = ParseDevices(Encoding.UTF8.GetString(frame));
                            Rebuild();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                    // 오류 표시는 mDNS 루프가 담당한다.
                }

                adbDevices = new Dictionary<string, string>();
                Rebuild();
                if (!await DelayAsync(RetryDelayMs, token)) return;
            }
        }

        private static Dictionary<string, string> ParseDevices(string text)
        {
            var map = new Dictionary<string, string>();
            foreach (var raw in text.Split('\n'))
            {
                var parts = raw.Trim().Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2) map[parts[0]] = parts[1];
            }
            return map;
        }

        // ---------------------------------------------------------------- 기기 묶기

        private void Rebuild()
        {
            var list = new List<WirelessDevice>();

            foreach (var service in services.Where(s => s.IsConnect))
            {
                var device = list.FirstOrDefault(d => d.Matches(service));
                if (device == null)
                {
                    device = new WirelessDevice { Key = KeyOf(service) };
                    list.Add(device);
                }
                if (device.Connect == null) device.Connect = service;
            }

            foreach (var service in services.Where(s => s.IsPairing && !s.IsQrPairing))
            {
                var device = list.FirstOrDefault(d => d.Matches(service));
                if (device == null)
                {
                    device = new WirelessDevice { Key = KeyOf(service) };
                    list.Add(device);
                }
                if (device.Pairing == null) device.Pairing = service;
            }

            foreach (var device in list)
                device.ConnectedSerial = FindConnectedSerial(device);

            list.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            devices = list;
            Changed?.Invoke();
        }

        private static string KeyOf(MdnsServiceInfo service)
        {
            string serial = service.DeviceSerial;
            return string.IsNullOrEmpty(serial) ? service.Host : serial;
        }

        private string FindConnectedSerial(WirelessDevice device)
        {
            string instance = device.Connect?.Instance;
            string host = device.Host;
            foreach (var pair in adbDevices)
            {
                if (pair.Value != "device") continue;
                string serial = pair.Key;
                // mDNS 자동 연결: "adb-{serial}-{rand}._adb-tls-connect._tcp", 수동 연결: "{ip}:{port}"
                if (!string.IsNullOrEmpty(instance) && serial.StartsWith(instance + ".", StringComparison.Ordinal)) return serial;
                if (!string.IsNullOrEmpty(host) && serial.StartsWith(host + ":", StringComparison.Ordinal)) return serial;
            }
            return null;
        }

        // ---------------------------------------------------------------- 공통

        private async Task<AdbConnection> OpenAsync(string service, CancellationToken token)
        {
            try
            {
                var connection = await AdbConnection.OpenAsync(service, token);
                unreachableSince = DateTime.MaxValue;
                return connection;
            }
            catch (SocketException)
            {
                var now = DateTime.UtcNow;
                if (unreachableSince == DateTime.MaxValue) unreachableSince = now;
                if (now - unreachableSince < StartServerAfter) throw;
                // platform-tools 설치 중에 서버를 띄우면 adb.exe가 잠겨(Windows) 설치가 실패한다.
                if (AndroidSdk.IsInstallingPlatformTools) throw;

                // 계속 없으면 한 번만 띄우고(두 루프가 공유) 다시 시도한다.
                if (ensureServerTask == null || ensureServerTask.IsCompleted)
                    ensureServerTask = AdbServer.EnsureRunningAsync(token);
                string error = await ensureServerTask;
                if (error != null) throw new InvalidOperationException(error);
                return await AdbConnection.OpenAsync(service, token);
            }
        }

        private static string Describe(Exception e)
        {
            if (e is SocketException || e is System.IO.IOException) return Localization.Get("error.noServer");
            return e.Message;
        }

        private static async Task<bool> DelayAsync(int ms, CancellationToken token)
        {
            try
            {
                await Task.Delay(ms, token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
