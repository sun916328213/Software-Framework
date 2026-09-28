using Secs4Net;
using System;
using System.Buffers.Binary;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance.Buffers;

namespace SecsGem.Runtime.Protocol.Building
{
    internal class HsmsMessageHeaderBuilder
    {
       static object _lock = new object();
        private  int Id { get; set; }
        private ushort DeviceId { get; set; }
        private MessageType MessageType { get; set; }
        private byte S { get; set; }
        private byte F { get; set; }
        private bool ReplyExpected { get; set; }

        /// <summary>
        /// Secs Message Header 获取反馈S9系列数据
        /// </summary>
        /// <param name="secsMessage"></param>
        /// <param name="DeviceId"></param>
        /// <param name="ID"></param>
        /// <returns></returns>
        internal static byte[] GetHeaderInfo(SecsMessage secsMessage, ushort DeviceId, int ID)
        {
            lock (_lock)
            {
                var headerBytes = new byte[10];
                var buffer = new MemoryBufferWriter<byte>(headerBytes);
                new HsmsMessageHeaderBuilder
                {
                    DeviceId = DeviceId,
                    ReplyExpected = secsMessage.ReplyExpected,
                    S = secsMessage.S,
                    F = secsMessage.F,
                    MessageType = MessageType.DataMessage,
                    Id = ID
                }.EncodeTo(buffer);
                return headerBytes;
            }
        }

        private void EncodeTo(IBufferWriter<byte> buffer)
        {
            var span = buffer.GetSpan(sizeHint: 10);
            BinaryPrimitives.WriteUInt16BigEndian(span, DeviceId);
            ref var r0 = ref MemoryMarshal.GetReference(span);
            Unsafe.Add(ref r0, 2u) = (byte)(S | (ReplyExpected ? 0b1000_0000 : 0));
            Unsafe.Add(ref r0, 3u) = F;
            Unsafe.Add(ref r0, 4u) = 0;
            Unsafe.Add(ref r0, 5u) = (byte)MessageType;
 
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(6, 4), Id);

            buffer.Advance(10);
        }

        private static void Decode(ReadOnlySpan<byte> span, out HsmsMessageHeaderBuilder header)
        {
            ref var r0 = ref MemoryMarshal.GetReference(span);
            var s = Unsafe.Add(ref r0, 2u);
            header = new HsmsMessageHeaderBuilder
            {
                DeviceId = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(span) & 0b01111111_11111111),
                ReplyExpected = (s & 0b1000_0000) != 0,
                S = (byte)(s & 0b0111_1111),
                F = Unsafe.Add(ref r0, 3u),
                MessageType = (MessageType)Unsafe.Add(ref r0, 5u),
 
                Id = BinaryPrimitives.ReadInt32BigEndian(span.Slice(6, 4)),
            };
        }
    }
}
