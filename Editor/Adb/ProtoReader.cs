using System;
using System.Text;

namespace AndroidWireless
{
    /// <summary>adb 서버가 보내는 protobuf를 읽기 위한 최소 wire 포맷 디코더.</summary>
    internal struct ProtoReader
    {
        public const int WireVarint = 0;
        public const int WireFixed64 = 1;
        public const int WireLengthDelimited = 2;
        public const int WireFixed32 = 5;

        private readonly byte[] buffer;
        private readonly int end;
        private int position;

        public ProtoReader(byte[] buffer) : this(buffer, 0, buffer.Length) { }

        public ProtoReader(byte[] buffer, int offset, int length)
        {
            this.buffer = buffer;
            position = offset;
            end = offset + length;
        }

        public bool Next(out int field, out int wireType)
        {
            if (position >= end)
            {
                field = 0;
                wireType = 0;
                return false;
            }

            ulong key = ReadVarint();
            field = (int)(key >> 3);
            wireType = (int)(key & 7);
            return true;
        }

        public ulong ReadVarint()
        {
            ulong result = 0;
            for (int shift = 0; shift < 64; shift += 7)
            {
                if (position >= end) throw new FormatException("protobuf varint가 잘렸습니다.");
                byte b = buffer[position++];
                result |= (ulong)(b & 0x7F) << shift;
                if (b < 0x80) return result;
            }
            throw new FormatException("protobuf varint가 너무 깁니다.");
        }

        public ProtoReader ReadMessage()
        {
            int length = ReadLength();
            var sub = new ProtoReader(buffer, position, length);
            position += length;
            return sub;
        }

        public string ReadString()
        {
            int length = ReadLength();
            string value = Encoding.UTF8.GetString(buffer, position, length);
            position += length;
            return value;
        }

        public void Skip(int wireType)
        {
            switch (wireType)
            {
                case WireVarint: ReadVarint(); break;
                case WireFixed64: Advance(8); break;
                case WireLengthDelimited: Advance(ReadLength()); break;
                case WireFixed32: Advance(4); break;
                default: throw new FormatException($"지원하지 않는 protobuf wire type: {wireType}");
            }
        }

        private int ReadLength()
        {
            ulong length = ReadVarint();
            if (length > (ulong)(end - position)) throw new FormatException("protobuf 길이가 버퍼를 넘습니다.");
            return (int)length;
        }

        private void Advance(int count)
        {
            if (count > end - position) throw new FormatException("protobuf 필드가 잘렸습니다.");
            position += count;
        }
    }
}
