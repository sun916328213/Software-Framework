// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：HsmsMessageHeaderBuilder。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Runtime、SecsGem.Runtime.Protocol、Secs4Net、System、System.Buffers.Binary、System.Buffers。导入不代表所有类型都会在运行时加载。
using SecsGem.Runtime;
using SecsGem.Runtime.Protocol;
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
    /// <summary>
    /// HsmsMessageHeaderBuilder 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EncodeTo。
        /// 正文中的关键协作调用包括 GetSpan、WriteUInt16BigEndian、GetReference、Add、WriteInt32BigEndian；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Decode。
        /// 正文中的关键协作调用包括 GetReference、Add、ReadUInt16BigEndian、ReadInt32BigEndian、Slice；具体条件和先后次序见方法体。
        /// </summary>
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
