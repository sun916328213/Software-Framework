using Communication.Communication.Socket;
using Communication.Core.Definition;
using Communication.Tools.Stream;
using Communication.Tools.Stream.FrameSplitter.Splitter;
using Extension.Core.Comparison;
using Extension.Core.Conversion;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static Communication.Core.Definition.CommunicationParam;
using static Communication.Protocols.Mitsubishi.Mc3EBinaryPacketBuilder;


namespace Communication.Protocols.Mitsubishi
{
    public class MC3EHelper :MCBase
    {
        IContainerProvider  containerProvider;
        public MC3EHelper(IContainerProvider container):base(container) 
        {
            containerProvider=  container ;
        }

        /// <summary>
        /// 用于交互的端口
        /// </summary>
        protected ICommPort mClient;

        // 用于生成并匹配请求ID ，（发一个等一个），这是最安全的做法。
        private readonly SemaphoreSlim mLock = new SemaphoreSlim(1, 1);

        private TaskCompletionSource<byte[]> mCurrentTcs;

        /// <summary>
        /// 用于生成请求报文
        /// </summary>
        Mc3EComposerBuilder mBuilder = new Mc3EComposerBuilder();


        public override DeviceProtocolType CommunicationType { get; } = DeviceProtocolType.MC3E;
        public override bool IsConnected { get =>  mClient.IsOpen; set => throw new NotImplementedException(); }
 

        public void Config(string url) 
        {
            string name= url;
            mClient= containerProvider.Resolve<CommPortManager>().GetPort(name);

            //mClient.FrameSplitter = mSplitter;
            mClient.OnDisconnect += (sender) =>
            {
                // 如果连接断开了，通知等待的任务抛出异常
                mCurrentTcs?.TrySetException(new Exception("Socket disconnected unexpectedly."));
            };

            ModuleSplitter moduleSplitter = new Tools.Stream.FrameSplitter.Splitter.ModuleSplitter()
                .Add(new Tools.Stream.FrameSplitter.SplitModules.HeaderModule(new byte[] { 0xD0, 0x00 }))
                .Add(new Tools.Stream.FrameSplitter.SplitModules.SizeModule(5))
                .Add(new Tools.Stream.FrameSplitter.SplitModules.LengthModule());

            // 当拆出一帧完整包时：交付给等待方
            moduleSplitter.FrameCompleted += frame =>
            {
                byte[] data = frame.ToArray();
                if (mCurrentTcs == null)
                    return;                 // 没人在等：迟到包/残留包 -> 丢弃
                                            // B. 优先检查是否是异步请求
                if (mCurrentTcs != null && !mCurrentTcs.Task.IsCompleted)
                {
                    // 异步模式：设置 Task 结果，让 await 继续运行
                    mCurrentTcs.TrySetResult(data);
                }

            };

            mClient.FrameSplitter = moduleSplitter;


        }
 
        public override Task Connect()
        {
            if (!mClient.IsOpen)
            {
                mClient.Connect();
            }
            return Task.CompletedTask;
        }

        public override void Disconnect()
        {
            if (mClient.IsOpen)
            {
                mClient.Disconnect();
            }
        }
 
        public override async Task ReadAsync(CancellationToken ct = default)
        {
            // 遍历所有已分类和排序的读取块
            foreach (var kvp in mReadList)
            {
                E_McDeviceCode code = kvp.Key;
                List<AddressBlock> blocks = kvp.Value;

                foreach (var block in blocks)
                {
                    await ReadValue(code, block.StartAddress, block.Length,true);
                }
            }
        }

        public override async Task<DeviceValue> ReadAsync(ICommunicationDataPoint dp, CancellationToken ct = default)
        {
            //手动读取单个点，不进行自动回调，与数据更新

            McDataPoint mp = dp as McDataPoint;
            E_McDeviceCode code = mp.DecodeData.Area;

            int startAddr = mp.DecodeData.Address;

            int readLength = mp.GetLength();

            await ReadValue(code, startAddr,readLength,false);

            return dp.GetValue();
        }

        public override   void Write(params (ICommunicationDataPoint dp, object value)[] pvs)
        {
            Task.Run(async () => await WriteValue(pvs)).GetAwaiter().GetResult();
        }

        public override void Write(ICommunicationDataPoint dp, object value)
        {
            Task.Run(async () => await WriteValue((dp, value))).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(params (ICommunicationDataPoint dp, object value)[] pvs)
        {
          await  WriteValue(pvs);
        }

        public override async Task WriteAsync(ICommunicationDataPoint dp, object value)
        {
            await WriteValue((dp, value));
        }


        /// <summary>
        /// 读取并解析数据
        /// </summary>
        /// <exception cref="Exception"></exception>
        private async Task ReadValue(E_McDeviceCode code, int startAddr, int readLength,bool IsUpdate)
        {
            // 1. 构建读取报文
            byte[] request = mBuilder.BuildRead(code, startAddr, readLength);

            // 2. 发送并等待响应
            byte[] response = await mClient.WriteRequestAsync(request);//SendAndWaitAsync(request);

            ushort successCode = response.ToUInt16(9);
            if (successCode != 0)//读取失败
            {
                throw new Exception($"获取异常代码：{successCode}");
            }
            else
            {
                // 3. 解析数据并填充到 mValueGroup
                switch (code)
                {
                    case E_McDeviceCode.M:
                    case E_McDeviceCode.X:
                    case E_McDeviceCode.Y:
                    case E_McDeviceCode.B:
                        byte[] bv = new byte[response.Length - 11];
                        Buffer.BlockCopy(response, 11, bv, 0,bv.Length);
                        bool[] values = bv.ToHexBools();
                        ////进行比较,复制，触发变更
                        ValueCompare_Bool(code, (bool[])mValueGroup[code], startAddr, values, IsUpdate);
                        break;
                    case E_McDeviceCode.D:
                    case E_McDeviceCode.W:
                    case E_McDeviceCode.R:
                        ValueCompare_Byte(code, (byte[])mValueGroup[code], startAddr*2, response, 11, IsUpdate);
                        break;
                    default:
                        break;
                }
            }
        }

        private async Task WriteValue(params (ICommunicationDataPoint dp, object value)[] pvs)
        {
            List<(McDataPoint dp, byte[] data)> wordItems = new List<(McDataPoint dp, byte[] data)>();
            List<(McDataPoint dp, byte[] data)> bitItems = new List<(McDataPoint dp, byte[] data)>();
            foreach (var dp0 in pvs)
            {
                McDataPoint dp = dp0.dp as McDataPoint;
                object value = dp0.value;
                switch (dp.DataType)
                {
                    case DataType.BIT:
                        byte[] bd = ValueDecoder.Code(value, dp.DataType, (ushort)(dp.GetLength() * 2));
                        bitItems.Add((dp, bd));
                        break;
                    case DataType.BYTE:
                        break;
                    case DataType.INT16:
                    case DataType.UINT16:
                    case DataType.INT32:
                    case DataType.UINT32:
                    case DataType.SINGLE:
                    case DataType.DOUBLE:
                    case DataType.UTF32:
                    case DataType.ASCII:
                        byte[] d = ValueDecoder.Code(value, dp.DataType, (ushort)(dp.GetLength() * 2));
                        wordItems.Add((dp, d));
                        break;
                    default:
                        break;
                }
            }
            // 1. 构建读取报文
            byte[] request = mBuilder.BuildWrite(wordItems, bitItems);

            // 2. 发送并等待响应
            byte[] response = await mClient.WriteRequestAsync(request);//SendAndWaitAsync(request);

            // 4. 校验结果
            ushort successCode = response.ToUInt16(9);
            if (successCode != 0)//读取失败
            {
                throw new Exception($"写入失败,获取异常代码：{successCode}");
            }
        }

    }

    #region 报文格式创建工具
    /// <summary>
    /// 收发代码生成工具
    /// </summary>
    sealed class Mc3EBinaryPacketBuilder
    {
        public class Mc3EComposerBuilder
        {
            // 配置项
            private byte[] SubHeader = { 0x50, 0x00 }; // 固定为 0x0050 (请求)
            public byte NetworkNo { get; set; } = 0x00;
            public byte PcNo { get; set; } = 0xFF;
            public byte[] IoNo { get; set; } = { 0xFF, 0x03 };// 0x03FF;
            public byte StationNo { get; set; } = 0x00;
            public byte[] MonitorTimer { get; set; } = { 0x10, 0x00 };//0x0010;
            private byte[] ReadCommd = { 0x01, 0x04 }; // 批量读块
            private byte[] WriteCommd = { 0x06, 0x14 }; // 随机写入

            /// <summary>
            /// 使用 Composer 自动生成读取报文
            /// 批量读取
            /// </summary>
            public byte[] BuildRead(E_McDeviceCode code, int startAddr, int points)
            {
                // 根据设备类型决定子命令
                bool isBit = IsBitDevice(code);
                byte[] subCmd = isBit ? new byte[] { 0x01,0x00 } : new byte[] { 0x00, 0x00 };

                var composer = new Tools.Stream.FrameComposer();

                // --- 1. 3E 帧头 (Header) ---
                composer.Add(new ConstBytesModule(SubHeader))                       // SubHeader (Req)
                        .Add(new ConstBytesModule(new byte[]{ NetworkNo }))         // Network
                        .Add(new ConstBytesModule(new byte[] { PcNo }))             // PC
                        .Add(new ConstBytesModule(IoNo))                            // IO
                        .Add(new ConstBytesModule(new byte[] { StationNo }));       // Station

                // --- 2. 长度自计算区域 ---
                // MC协议规定：长度字段 = 从 MonitorTimer 开始到最后的所有字节数
                // 定义：计算从 "DataStart" 到 "DataEnd" 的长度，填入2字节(小端字节序)
                composer.Add(new LengthModule("DataStart", "DataEnd", 2));    //Length

                // --- 3. 数据体 (Command Body) ---
                composer.Add(new MarkModule( MarkType.Length,"DataStart"))                           // <--- 长度计算起点

                        .Add(new ConstBytesModule(MonitorTimer))                    // Timer
                        .Add(new ConstBytesModule(ReadCommd))                       // Command (Batch Read)
                        .Add(new ConstBytesModule(subCmd))                          // SubCommand
                        .Add(new ConstBytesModule(startAddr.ToBytes( EEndianType.Little_Endian).Take(3).ToArray()))            // Address (3 Bytes)
                        .Add(new ConstBytesModule(new byte[] { (byte)code }))       // Device Code
                        .Add(new ConstBytesModule(points.ToBytes(EEndianType.Little_Endian).Take(2).ToArray()))                // Points

                        .Add(new MarkModule(MarkType.Length, "DataEnd"));                            // <--- 长度计算终点

                // --- 4. 生成 ---
                return composer.Build();
            }

            /// <summary>
            /// 使用 Composer 自动生成写入报文
            /// </summary>
            public byte[] BuildWrite(List<(McDataPoint dp, byte[] data)> wordItems, List<(McDataPoint dp, byte[] data)> bitItems)
            {
                if ((wordItems == null && bitItems == null) || 
                    (wordItems.Count == 0 && bitItems.Count == 0)) 
                    return null;

                // 检查数量上限 (根据实际PLC型号调整，这里假设一般限制)
                if (wordItems.Count + bitItems.Count > 192) throw new ArgumentException("随机写入点数过多，最多一次写入192字，请分包处理");


                // 1) 构建报文
                var composer = new Tools.Stream.FrameComposer();

                // --- Header ---
                composer.Add(new ConstBytesModule(SubHeader))
                        .Add(new ConstBytesModule(new byte[] { NetworkNo }))
                        .Add(new ConstBytesModule(new byte[] { PcNo }))
                        .Add(new ConstBytesModule(IoNo))
                        .Add(new ConstBytesModule(new byte[] { StationNo }));

                // --- Length ---
                composer.Add(new LengthModule("DataStart", "DataEnd", 2));

                // --- Body ---
                composer.Add(new MarkModule(MarkType.Length, "DataStart"))
                        .Add(new ConstBytesModule(MonitorTimer))
                        .Add(new ConstBytesModule(WriteCommd))                 // 0x1406
                        .Add(new ConstBytesModule(new byte[] { 0x00, 0x00 })) // SubCommand (一般为 0x0000)

                        //// 块数（按常见实现：1字节）
                        .Add(new ConstBytesModule(wordItems.Count.ToBytes( EEndianType.Little_Endian).Take(1).ToArray()))           // Word block count
                        .Add(new ConstBytesModule(bitItems.Count.ToBytes( EEndianType.Little_Endian).Take(1).ToArray()));           // Bit  block count

                // 3) 追加 Word blocks
                foreach (var it in wordItems)
                {
                    AddBlockToComposer(composer, it.dp, it.data);
                }

                // 4) 追加 Bit blocks
                foreach (var it in bitItems)
                {
                    AddBlockToComposer(composer, it.dp, it.data);
                }

                composer.Add(new MarkModule(MarkType.Length, "DataEnd"));

                return composer.Build();
            }

            /// <summary>
            /// 添加单个块
            /// </summary>
            private void AddBlockToComposer(FrameComposer composer, McDataPoint dp, byte[] data)
            {
                // 计算字数 (Word Count)
                // 假设 data.Length 已经是偶数 (2字节=1字)
                int wordCount = data.Length / 2;

                // 校验：1406 单块最大 255 字
                if (wordCount > 255)
                    throw new ArgumentException($"1406指令单块数据不能超过255个字 (Code:{dp.DecodeData.Area}, Addr:{dp.DecodeData.Address})");

                if (wordCount == 0) return;

                // 结构：[Code(1)] + [Address(3)] + [Point(1)] + [Data(N)]
                composer.Add(new ConstBytesModule(((int)dp.DecodeData.Address).ToBytes(EEndianType.Little_Endian).Take(3).ToArray()))        // 1. Address
                        .Add(new ConstBytesModule(new byte[] { (byte)dp.DecodeData.Area })) // 2. Code
                        .Add(new ConstBytesModule(wordCount.ToBytes(EEndianType.Little_Endian).Take(2).ToArray()))          // 3. Point 
                        .Add(new ConstBytesModule(data));                                   // 4. Data
            }

            private bool IsBitDevice(E_McDeviceCode code)
            {
                return code == E_McDeviceCode.M || code == E_McDeviceCode.X || code == E_McDeviceCode.Y || code == E_McDeviceCode.B;
            }
        }
    }
    #endregion
}
