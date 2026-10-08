using Secs4Net;
using System;
using System.Buffers.Binary;

namespace SecsGem.Runtime.Protocol.Building
{
    /// <summary>编码用于S9系列反馈的10字节HSMS报头。</summary>
    internal static class HsmsMessageHeaderBuilder
    {
        internal static byte[] GetHeaderInfo(SecsMessage secsMessage, ushort DeviceId, int ID)
        {
            var header = new byte[10];
            BinaryPrimitives.WriteUInt16BigEndian(header, DeviceId);
            header[2] = (byte)(secsMessage.S | (secsMessage.ReplyExpected ? 0x80 : 0));
            header[3] = secsMessage.F;
            // PType和DataMessage的SType均为0，数组已初始化。
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(6, 4), ID);
            return header;
        }
    }
}
