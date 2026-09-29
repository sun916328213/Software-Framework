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

        public InovanceModbusTransport()
            : this(new ModbusTcpClient())
        {
        }

        internal InovanceModbusTransport(ModbusTcpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public bool IsConnected => _client.IsConnected;

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

        public void Disconnect() => _client.Disconnect();

        public bool TryReadStatus(int axis, out ushort status)
        {
            status = 0;
            var values = _client.ReadHoldingRegisters(_slaveId, AxisAddress(axis, 1), 1);
            if (values == null || values.Length < 1)
                return false;
            status = unchecked((ushort)values[0]);
            return true;
        }

        public bool UpdateControlWord(int axis, ushort setBits, ushort clearMask)
        {
            var address = AxisAddress(axis, 0);
            var values = _client.ReadHoldingRegisters(_slaveId, address, 1);
            if (values == null || values.Length < 1)
                return false;
            var word = (ushort)((values[0] & ~clearMask) | setBits);
            return _client.WriteSingleRegister(_slaveId, address, unchecked((short)word));
        }

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

        public bool TryReadCurrentPosition(int axis, out int position)
        {
            position = 0;
            var values = _client.ReadHoldingRegisters(_slaveId, AxisAddress(axis, 6), 2);
            if (values == null || values.Length < 2)
                return false;
            position = (values[1] << 16) | unchecked((ushort)values[0]);
            return true;
        }

        public bool SetCurrentPosition(int axis, int position)
            => _client.WriteMultipleRegisters(_slaveId, AxisAddress(axis, 6),
            [
                unchecked((short)position),
                unchecked((short)(position >> 16)),
            ]);

        public bool TryReadInput(int index, out bool state)
            => TryReadIo(InputOffset, index, out state);

        public bool TryReadOutput(int index, out bool state)
            => TryReadIo(OutputOffset, index, out state);

        public bool WriteOutput(int index, bool state)
        {
            if (!TryIoAddress(OutputOffset, index, out var address))
                return false;
            return _client.WriteSingleRegister(_slaveId, address, (short)(state ? 1 : 0));
        }

        public void Dispose() => Disconnect();

        private ushort AxisAddress(int axis, int offset)
            => checked((ushort)(_registerBase + axis * _registerStride + offset));

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

