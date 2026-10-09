using System;
using System.Collections.Generic;

namespace AndroidWireless
{
    /// <summary>
    /// adb 서버의 `host:track-mdns-services`가 알려주는 mDNS 서비스 하나 (adb.proto.MdnsService).
    /// 구버전 adb 폴백(`host:mdns:services`)에서는 이름·타입·주소만 채워진다.
    /// </summary>
    public sealed class MdnsServiceInfo
    {
        public const string ConnectType = "_adb-tls-connect";
        public const string PairingType = "_adb-tls-pairing";
        public const string QrInstancePrefix = "studio-";

        public string Instance = "";
        public string Service = "";
        public string Domain = "";
        public string IPv4 = "";
        public readonly List<string> IPv6 = new List<string>();
        public int Port;
        public string ProductModel = "";
        public string SdkVersion = "";
        public string GivenName = "";
        public string Serial = "";
        public string ServiceVersion = "";
        public string Hostname = "";

        public bool IsPairing => Service.StartsWith(PairingType, StringComparison.Ordinal);
        public bool IsConnect => Service.StartsWith(ConnectType, StringComparison.Ordinal);

        /// <summary>QR 페어링을 위해 이 창이 만든 서비스 이름(studio-…)인지.</summary>
        public bool IsQrPairing => IsPairing && Instance.StartsWith(QrInstancePrefix, StringComparison.Ordinal);

        public string Host => !string.IsNullOrEmpty(IPv4) ? IPv4 : (IPv6.Count > 0 ? IPv6[0] : "");

        public string Address
        {
            get
            {
                string host = Host;
                if (host.Contains(":")) host = "[" + host + "]";
                return $"{host}:{Port}";
            }
        }

        /// <summary>serial 필드가 비어 있으면 인스턴스 이름(adb-{serial}-{random})에서 꺼낸다.</summary>
        public string DeviceSerial
        {
            get
            {
                if (!string.IsNullOrEmpty(Serial)) return Serial;
                if (!Instance.StartsWith("adb-", StringComparison.Ordinal)) return "";
                int last = Instance.LastIndexOf('-');
                return last > 4 ? Instance.Substring(4, last - 4) : "";
            }
        }

        /// <summary>ADB Wi-Fi 버전(1 = v1.0, 2 = v2.0). 알 수 없으면 0.</summary>
        public int AdbWifiVersion => int.TryParse(ServiceVersion, out int v) ? v : 0;

        // ---------------------------------------------------------------- 파싱

        /// <summary>
        /// adb.proto.MdnsServices: 서비스 종류별 반복 필드 안에 MdnsService가 필드 1로 들어 있다.
        /// 종류는 MdnsService.service에 다시 들어 있으므로 바깥 필드 번호에 의존하지 않는다.
        /// </summary>
        public static List<MdnsServiceInfo> ParseServices(byte[] payload)
        {
            var list = new List<MdnsServiceInfo>();
            var reader = new ProtoReader(payload);
            while (reader.Next(out _, out int wire))
            {
                if (wire != ProtoReader.WireLengthDelimited)
                {
                    reader.Skip(wire);
                    continue;
                }

                var wrapper = reader.ReadMessage();
                while (wrapper.Next(out int field, out int innerWire))
                {
                    if (field == 1 && innerWire == ProtoReader.WireLengthDelimited)
                        list.Add(Parse(wrapper.ReadMessage()));
                    else
                        wrapper.Skip(innerWire);
                }
            }
            return list;
        }

        private static MdnsServiceInfo Parse(ProtoReader reader)
        {
            var info = new MdnsServiceInfo();
            while (reader.Next(out int field, out int wire))
            {
                if (wire == ProtoReader.WireVarint)
                {
                    ulong value = reader.ReadVarint();
                    if (field == 6) info.Port = (int)value;
                    continue;
                }
                if (wire != ProtoReader.WireLengthDelimited)
                {
                    reader.Skip(wire);
                    continue;
                }

                switch (field)
                {
                    case 1: info.Instance = reader.ReadString(); break;
                    case 2: info.Service = reader.ReadString(); break;
                    case 3: info.Domain = reader.ReadString(); break;
                    case 4: info.IPv4 = reader.ReadString(); break;
                    case 5: info.IPv6.Add(reader.ReadString()); break;
                    case 7: info.ProductModel = reader.ReadString(); break;
                    case 8: info.SdkVersion = reader.ReadString(); break;
                    case 9: info.GivenName = reader.ReadString(); break;
                    case 10: info.Serial = reader.ReadString(); break;
                    case 11: info.ServiceVersion = reader.ReadString(); break;
                    case 12: info.Hostname = reader.ReadString(); break;
                    default: reader.Skip(wire); break;
                }
            }
            return info;
        }

        /// <summary>`host:mdns:services` 텍스트. 한 줄: "adb-R58M…-AbCd\t_adb-tls-connect._tcp\t192.168.0.5:37011"</summary>
        public static List<MdnsServiceInfo> ParseText(string text)
        {
            var list = new List<MdnsServiceInfo>();
            foreach (var raw in text.Split('\n'))
            {
                var parts = raw.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3 || !parts[1].StartsWith("_adb", StringComparison.Ordinal)) continue;

                string address = parts[2];
                int colon = address.LastIndexOf(':');
                if (colon <= 0 || !int.TryParse(address.Substring(colon + 1), out int port)) continue;

                var info = new MdnsServiceInfo { Instance = parts[0], Service = parts[1], Port = port };
                string host = address.Substring(0, colon).Trim('[', ']');
                if (host.Contains(":")) info.IPv6.Add(host);
                else info.IPv4 = host;
                list.Add(info);
            }
            return list;
        }
    }
}
