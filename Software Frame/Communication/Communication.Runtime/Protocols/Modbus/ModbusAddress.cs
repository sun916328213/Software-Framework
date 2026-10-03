// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusRegion、ModbusAddress。成员说明可结合下方具体实现阅读。
namespace Communication.Runtime.Protocols.Modbus
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
        /// <summary>解析强类型地址，并将调用方数量转换为协议元素数量。</summary>
        public static (ModbusAddressValue address, ushort quantity) ParseValue(
            string address,
            int length)
        {
            if (length is <= 0 or > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "数量必须在 1 到 65535 之间。");
            }

            return (ModbusAddressValue.Parse(address), (ushort)length);
        }
    }
}
