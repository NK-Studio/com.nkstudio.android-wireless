using System;

namespace AndroidWireless
{
    /// <summary>같은 기기가 알리는 연결(_adb-tls-connect)·페어링(_adb-tls-pairing) 서비스를 하나로 묶은 것.</summary>
    public sealed class WirelessDevice
    {
        public string Key;
        public MdnsServiceInfo Connect;
        public MdnsServiceInfo Pairing;

        /// <summary>adb에 이미 연결돼 있으면 그 serial. 아니면 null.</summary>
        public string ConnectedSerial;

        private MdnsServiceInfo Primary => Connect ?? Pairing;

        public string Serial => Primary?.DeviceSerial ?? "";
        public string Host => Primary?.Host ?? "";
        public string Address => Primary?.Address ?? "";
        public bool IsConnected => ConnectedSerial != null;

        /// <summary>v2 기기가 알리는 사용자 지정 이름 → 모델명 → serial → IP 순.</summary>
        public string DisplayName => FirstNonEmpty(Connect?.GivenName, Pairing?.GivenName, Model, Serial, Host);

        public string Model => FirstNonEmpty(Connect?.ProductModel, Pairing?.ProductModel);
        public string Api => FirstNonEmpty(Connect?.SdkVersion, Pairing?.SdkVersion);
        public int AdbWifiVersion => Math.Max(Connect?.AdbWifiVersion ?? 0, Pairing?.AdbWifiVersion ?? 0);

        public bool Matches(MdnsServiceInfo service)
        {
            string serial = Serial;
            if (!string.IsNullOrEmpty(serial) && service.DeviceSerial == serial) return true;
            string host = Host;
            return !string.IsNullOrEmpty(host) && service.Host == host;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var v in values)
                if (!string.IsNullOrEmpty(v)) return v;
            return "";
        }
    }
}
