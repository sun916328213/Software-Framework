namespace CommunicationNew.Protocols.Modbus
{
    /// <summary>
    /// Modbus 地址类型
    /// </summary>
    public enum ModbusRegion
    {
        /// <summary>线圈 (0x, Function Code 01/05/15)</summary>
        Coil = 0,
        /// <summary>离散输入 (1x, Function Code 02)</summary>
        DiscreteInput = 1,
        /// <summary>输入寄存器 (3x, Function Code 04)</summary>
        InputRegister = 3,
        /// <summary>保持寄存器 (4x, Function Code 03/06/16)</summary>
        HoldingRegister = 4,
    }

    /// <summary>
    /// Modbus 地址解析器 —— 将字符串地址解析为 (Region, StartAddress, Quantity)
    /// 支持格式：
    ///   "HR1" / "4x001" / "40001"  → HoldingRegister, address=0
    ///   "IR1" / "3x001" / "30001"  → InputRegister, address=0
    ///   "C1"  / "0x001" / "00001"  → Coil, address=0
    ///   "DI1"/ "1x001" / "10001"  → DiscreteInput, address=0
    /// </summary>
    public static class ModbusAddress
    {
        public static (ModbusRegion region, ushort startAddress, ushort quantity) Parse(string address, int length)
        {
            if (length is <= 0 or > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "数量必须在 1 到 65535 之间。");
            }

            var value = ModbusAddressValue.Parse(address);
            return (value.Region, value.StartAddress, (ushort)length);
        }
    }
}
