using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace AndroidWireless.Tests
{
    /// <summary>
    /// adb 37의 `host:track-mdns-services` 프레임 필드 매핑 회귀 테스트.
    /// 실제 기기(SM-S906N, API 36.0, ADB Wi-Fi v1)에서 받은 프레임과 같은 구조로 만든다.
    /// </summary>
    public class MdnsServiceParsingTests
    {
        [Test]
        public void ParsesTrackMdnsServicesFrame()
        {
            var service = new Proto()
                .Str(1, "adb-R3CT80XL7KX-dtyd0A")
                .Str(2, "_adb-tls-connect._tcp")
                .Str(4, "192.168.0.69")
                .Str(5, "fe80:0:0:0:c4d2:e8ff:fec2:6b21")
                .Varint(6, 37453)
                .Str(7, "SM-S906N")
                .Str(8, "36.0")
                .Str(11, "1")
                .Str(12, "Android-2.local");
            var frame = new Proto().Msg(2, new Proto().Msg(1, service)).ToArray();

            var list = MdnsServiceInfo.ParseServices(frame);

            Assert.AreEqual(1, list.Count);
            var s = list[0];
            Assert.IsTrue(s.IsConnect);
            Assert.AreEqual("192.168.0.69:37453", s.Address);
            Assert.AreEqual("SM-S906N", s.ProductModel);
            Assert.AreEqual("36.0", s.SdkVersion);
            Assert.AreEqual(1, s.AdbWifiVersion);
            Assert.AreEqual("R3CT80XL7KX", s.DeviceSerial);
            Assert.AreEqual("Android-2.local", s.Hostname);
        }

        [Test]
        public void UnknownFieldsAreSkipped()
        {
            var service = new Proto().Str(1, "studio-abc").Str(2, "_adb-tls-pairing._tcp").Varint(99, 7).Str(4, "10.0.0.2").Varint(6, 5555).Str(11, "2");
            var frame = new Proto().Msg(3, new Proto().Msg(1, service).Varint(2, 1)).ToArray();

            var s = MdnsServiceInfo.ParseServices(frame)[0];

            Assert.IsTrue(s.IsQrPairing);
            Assert.AreEqual(2, s.AdbWifiVersion);
            Assert.AreEqual("10.0.0.2:5555", s.Address);
        }

        [Test]
        public void ParsesTextFallback()
        {
            var list = MdnsServiceInfo.ParseText("adb-R3CM608T0KW-eWSmgg\t_adb-tls-connect._tcp\t192.168.0.150:35139\n");

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("192.168.0.150:35139", list[0].Address);
            Assert.AreEqual("R3CM608T0KW", list[0].DeviceSerial);
            Assert.AreEqual(0, list[0].AdbWifiVersion);
        }

        /// <summary>테스트용 최소 protobuf 인코더.</summary>
        private sealed class Proto
        {
            private readonly List<byte> bytes = new List<byte>();

            public Proto Varint(int field, ulong value)
            {
                WriteVarint((ulong)(field << 3));
                WriteVarint(value);
                return this;
            }

            public Proto Str(int field, string value) => Bytes(field, Encoding.UTF8.GetBytes(value));
            public Proto Msg(int field, Proto message) => Bytes(field, message.ToArray());

            public byte[] ToArray() => bytes.ToArray();

            private Proto Bytes(int field, byte[] data)
            {
                WriteVarint((ulong)((field << 3) | 2));
                WriteVarint((ulong)data.Length);
                bytes.AddRange(data);
                return this;
            }

            private void WriteVarint(ulong value)
            {
                while (value >= 0x80)
                {
                    bytes.Add((byte)(value | 0x80));
                    value >>= 7;
                }
                bytes.Add((byte)value);
            }
        }
    }
}
