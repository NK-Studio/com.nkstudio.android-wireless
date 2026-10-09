using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AndroidWireless
{
    public readonly struct PairingOutcome
    {
        public readonly bool Success;
        public readonly string Serial;
        public readonly string DeviceName;
        public readonly string Message;

        private PairingOutcome(bool success, string serial, string deviceName, string message)
        {
            Success = success;
            Serial = serial;
            DeviceName = deviceName;
            Message = message;
        }

        public static PairingOutcome Ok(string serial, string deviceName) => new PairingOutcome(true, serial, deviceName, null);
        public static PairingOutcome Fail(string message) => new PairingOutcome(false, null, null, message);
    }

    /// <summary>adb pair → 연결 대기(mDNS 자동 연결, 안 되면 직접 connect)까지의 공통 흐름.</summary>
    public static class PairingSession
    {
        private const int PairTimeoutSeconds = 30;
        private const int ConnectTimeoutSeconds = 20;
        private const int ConnectRetrySeconds = 3;

        /// <summary>QR 페이로드. 휴대폰은 serviceName으로 mDNS 페어링 서비스를 연다.</summary>
        public static (string payload, string serviceName, string password) CreateQr()
        {
            // Android Studio와 같은 형식의 서비스 이름
            string serviceName = MdnsServiceInfo.QrInstancePrefix + RandomString(10);
            string password = RandomString(12);
            return ($"WIFI:T:ADB;S:{serviceName};P:{password};;", serviceName, password);
        }

        public static async Task<PairingOutcome> PairAndConnectAsync(
            WirelessDiscovery discovery, string pairAddress, string secret, string fallbackName, CancellationToken token)
        {
            bool paired;
            string guid, message;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(PairTimeoutSeconds));
                try
                {
                    (paired, guid, message) = await AdbServer.PairAsync(pairAddress, secret, timeout.Token);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    return PairingOutcome.Fail(Localization.Get("error.pairTimeout"));
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    return PairingOutcome.Fail(e.Message);
                }
            }

            if (!paired)
                return PairingOutcome.Fail(FirstLine(message));

            string host = HostOf(pairAddress);
            string serial = await WaitForConnectionAsync(discovery, guid, host, token);
            if (serial == null)
                return PairingOutcome.Fail(Localization.Get("error.notConnected"));

            return PairingOutcome.Ok(serial, ResolveName(discovery, guid, host) ?? fallbackName);
        }

        /// <summary>
        /// 페어링 후 adb 서버가 _adb-tls-connect 서비스로 자동 연결하기를 기다린다.
        /// 자동 연결이 늦으면 같은 기기의 연결 서비스로 직접 connect한다.
        /// </summary>
        private static async Task<string> WaitForConnectionAsync(WirelessDiscovery discovery, string guid, string host, CancellationToken token)
        {
            var deadline = DateTime.UtcNow.AddSeconds(ConnectTimeoutSeconds);
            var nextConnect = DateTime.UtcNow.AddSeconds(1);
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();

                string serial = FindConnectedSerial(discovery, guid, host);
                if (serial != null) return serial;

                if (DateTime.UtcNow >= nextConnect)
                {
                    nextConnect = DateTime.UtcNow.AddSeconds(ConnectRetrySeconds);
                    var service = discovery.Services.FirstOrDefault(s => s.IsConnect && (s.Instance == guid || s.Host == host));
                    if (service != null)
                    {
                        try
                        {
                            var (ok, _) = await AdbServer.ConnectAsync(service.Address, token);
                            if (ok) return service.Address;
                        }
                        catch (Exception e) when (!(e is OperationCanceledException))
                        {
                            // 다음 재시도에서 다시 확인한다.
                        }
                    }
                }

                await Task.Delay(500, token);
            }
            return null;
        }

        private static string FindConnectedSerial(WirelessDiscovery discovery, string guid, string host)
        {
            foreach (var pair in discovery.AdbDevices)
            {
                if (pair.Value != "device") continue;
                if (!string.IsNullOrEmpty(guid) && pair.Key.StartsWith(guid, StringComparison.Ordinal)) return pair.Key;
                if (pair.Key.StartsWith(host + ":", StringComparison.Ordinal)) return pair.Key;
            }
            return null;
        }

        private static string ResolveName(WirelessDiscovery discovery, string guid, string host)
        {
            var device = discovery.Devices.FirstOrDefault(d =>
                (d.Connect != null && d.Connect.Instance == guid) || d.Host == host);
            return device != null && !string.IsNullOrEmpty(device.DisplayName) ? device.DisplayName : null;
        }

        private static string HostOf(string address)
        {
            int colon = address.LastIndexOf(':');
            string host = colon > 0 ? address.Substring(0, colon) : address;
            return host.Trim('[', ']');
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Localization.Get("error.pairFailed");
            return text.Trim().Split('\n')[0];
        }

        private static string RandomString(int length)
        {
            const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);

            var sb = new StringBuilder(length);
            foreach (var b in bytes) sb.Append(alphabet[b % alphabet.Length]);
            return sb.ToString();
        }
    }
}
