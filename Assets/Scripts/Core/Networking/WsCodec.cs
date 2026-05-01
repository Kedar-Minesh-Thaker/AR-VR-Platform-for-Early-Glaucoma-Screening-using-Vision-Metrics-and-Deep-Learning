// WsCodec.cs
// Minimal RFC 6455 frame parse / build for Unity WebSocket server (client→server frames are masked).

using System;
using System.Collections.Generic;
using System.Text;

namespace OphthalSuite.Core
{
    public static class WsCodec
    {
        /// <summary>Try to remove one complete frame from buffer. Returns false if incomplete.</summary>
        public static bool TryConsumeFrame(List<byte> buf, out int opcode, out byte[] payload)
        {
            opcode = -1;
            payload = null;
            if (buf.Count < 2) return false;

            int b0 = buf[0];
            int b1 = buf[1];
            opcode = b0 & 0x0F;
            bool masked = (b1 & 0x80) != 0;
            long payloadLen = b1 & 0x7F;
            int pos = 2;

            if (payloadLen == 126)
            {
                if (buf.Count < 4) return false;
                payloadLen = (buf[2] << 8) | buf[3];
                pos = 4;
            }
            else if (payloadLen == 127)
            {
                if (buf.Count < 10) return false;
                payloadLen = 0;
                for (int i = 0; i < 8; i++)
                    payloadLen = (payloadLen << 8) | buf[2 + i];
                pos = 10;
            }

            if (payloadLen > 16 * 1024 * 1024) // guard
            {
                buf.Clear();
                return false;
            }

            if (masked)
            {
                if (buf.Count < pos + 4 + (int)payloadLen) return false;
                var mask = new byte[4] { buf[pos], buf[pos + 1], buf[pos + 2], buf[pos + 3] };
                pos += 4;
                payload = new byte[(int)payloadLen];
                for (int i = 0; i < (int)payloadLen; i++)
                    payload[i] = (byte)(buf[pos + i] ^ mask[i % 4]);
                pos += (int)payloadLen;
            }
            else
            {
                if (buf.Count < pos + (int)payloadLen) return false;
                payload = buf.GetRange(pos, (int)payloadLen).ToArray();
                pos += (int)payloadLen;
            }

            buf.RemoveRange(0, pos);
            return true;
        }

        public static byte[] EncodeControlFrame(byte opcode, byte[] payload)
        {
            int len = payload?.Length ?? 0;
            if (len > 125) throw new InvalidOperationException("Control frame too large");
            var frame = new byte[2 + len];
            frame[0] = (byte)(0x80 | opcode);
            frame[1] = (byte)len;
            if (len > 0) Buffer.BlockCopy(payload, 0, frame, 2, len);
            return frame;
        }
    }
}
