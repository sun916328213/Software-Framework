// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：InovanceModbusTransport。成员说明可结合下方具体实现阅读。
namespace MotionControl.Runtime.Vendors.Inovance
{
    /// <summary>将汇川轴语义映射为 Modbus TCP 寄存器操作的生产 adapter。</summary>
    internal sealed class InovanceModbusTransport : IInovanceTransport
    {
        private const int InputOffset = 10000;
        private const int OutputOffset = 11000;
        private readonly ModbusTcpClient _client;
        private byte _slaveId;
        private ushort _registerBase;
        private ushort _registerStride;

        /// <summary>
        /// 初始化 InovanceModbusTransport 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public InovanceModbusTransport()
            : this(new ModbusTcpClient())
        {
        }

        /// <summary>
        /// 初始化 InovanceModbusTransport 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
        /// </summary>
        internal InovanceModbusTransport(ModbusTcpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public bool IsConnected => _client.IsConnected;

        /// <summary>
        /// 发起连接操作；对应入口为 Connect。
        /// 正文中的关键协作调用包括 ThrowIfNull、Disconnect、Connect、TryReadStatus；具体条件和先后次序见方法体。
        /// </summary>
        public bool Connect(string ipAddress, int port, InovanceParam parameter, int requiredAxes)
        {
            ArgumentNullException.ThrowIfNull(parameter);
            Disconnect();
            if (requiredAxes <= 0 || !_client.Connect(ipAddress, port))
                return false;

            _slaveId = parameter.SlaveId;
            _registerBase = parameter.RegisterBase;
            _registerStride = parameter.RegisterStride;
            if (TryReadStatus(0, out _))
                return true;

            Disconnect();
            return false;
        }

        /// <summary>
        /// 执行断开连接操作；对应入口为 Disconnect。
        /// 正文中的关键协作调用包括 Disconnect；具体条件和先后次序见方法体。
        /// </summary>
        public void Disconnect() => _client.Disconnect();

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadStatus。
        /// 正文中的关键协作调用包括 ReadHoldingRegisters、AxisAddress；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryReadStatus(int axis, out ushort status)
        {
            status = 0;
            var values = _client.ReadHoldingRegisters(_slaveId, AxisAddress(axis, 1), 1);
            if (values == null || values.Length < 1)
                return false;
            status = unchecked((ushort)values[0]);
            return true;
        }

        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 UpdateControlWord。
        /// 正文中的关键协作调用包括 AxisAddress、ReadHoldingRegisters、WriteSingleRegister；具体条件和先后次序见方法体。
        /// </summary>
        public bool UpdateControlWord(int axis, ushort setBits, ushort clearMask)
        {
            var address = AxisAddress(axis, 0);
            var values = _client.ReadHoldingRegisters(_slaveId, address, 1);
            if (values == null || values.Length < 1)
                return false;
            var word = (ushort)((values[0] & ~clearMask) | setBits);
            return _client.WriteSingleRegister(_slaveId, address, unchecked((short)word));
        }

        /// <summary>
        /// 执行写入操作；对应入口为 WriteTarget。
        /// 正文中的关键协作调用包括 SingleToInt32Bits、WriteMultipleRegisters、AxisAddress；具体条件和先后次序见方法体。
        /// </summary>
        public bool WriteTarget(int axis, int position, float velocity)
        {
            var velocityBits = BitConverter.SingleToInt32Bits(velocity);
            return _client.WriteMultipleRegisters(_slaveId, AxisAddress(axis, 2),
            [
                unchecked((short)position),
                unchecked((short)(position >> 16)),
                unchecked((short)velocityBits),
                unchecked((short)(velocityBits >> 16)),
            ]);
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadCurrentPosition。
        /// 正文中的关键协作调用包括 ReadHoldingRegisters、AxisAddress；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryReadCurrentPosition(int axis, out int position)
        {
            position = 0;
            var values = _client.ReadHoldingRegisters(_slaveId, AxisAddress(axis, 6), 2);
            if (values == null || values.Length < 2)
                return false;
            position = (values[1] << 16) | unchecked((ushort)values[0]);
            return true;
        }

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetCurrentPosition。
        /// 正文中的关键协作调用包括 WriteMultipleRegisters、AxisAddress；具体条件和先后次序见方法体。
        /// </summary>
        public bool SetCurrentPosition(int axis, int position)
            => _client.WriteMultipleRegisters(_slaveId, AxisAddress(axis, 6),
            [
                unchecked((short)position),
                unchecked((short)(position >> 16)),
            ]);

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadInput。
        /// 正文中的关键协作调用包括 TryReadIo；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryReadInput(int index, out bool state)
            => TryReadIo(InputOffset, index, out state);

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadOutput。
        /// 正文中的关键协作调用包括 TryReadIo；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryReadOutput(int index, out bool state)
            => TryReadIo(OutputOffset, index, out state);

        /// <summary>
        /// 执行写入操作；对应入口为 WriteOutput。
        /// 正文中的关键协作调用包括 TryIoAddress、WriteSingleRegister；具体条件和先后次序见方法体。
        /// </summary>
        public bool WriteOutput(int index, bool state)
        {
            if (!TryIoAddress(OutputOffset, index, out var address))
                return false;
            return _client.WriteSingleRegister(_slaveId, address, (short)(state ? 1 : 0));
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Disconnect；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Disconnect();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AxisAddress。
        /// </summary>
        private ushort AxisAddress(int axis, int offset)
            => checked((ushort)(_registerBase + axis * _registerStride + offset));

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadIo。
        /// 正文中的关键协作调用包括 TryIoAddress、ReadHoldingRegisters；具体条件和先后次序见方法体。
        /// </summary>
        private bool TryReadIo(int offset, int index, out bool state)
        {
            state = false;
            if (!TryIoAddress(offset, index, out var address))
                return false;
            var values = _client.ReadHoldingRegisters(_slaveId, address, 1);
            if (values == null || values.Length < 1)
                return false;
            state = values[0] != 0;
            return true;
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryIoAddress。
        /// </summary>
        private bool TryIoAddress(int offset, int index, out ushort address)
        {
            var candidate = (long)_registerBase + offset + index;
            if (index < 0 || candidate > ushort.MaxValue)
            {
                address = 0;
                return false;
            }
            address = (ushort)candidate;
            return true;
        }
    }
}

