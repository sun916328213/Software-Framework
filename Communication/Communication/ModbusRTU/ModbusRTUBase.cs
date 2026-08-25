using Communication.Struct;
using Extension.Helper.Channel;

namespace Communication.ModbusRTU
{
    public abstract class ModbusRTUBase
    {
        /// <summary>
        /// 串口参数
        /// </summary>
        public abstract ModbusRTUParam ModbusRTUParam { get; set; }
        /// <summary>
        /// 数据集合
        /// </summary>
        internal abstract ChannelHelper<byte[]> Datas { get; set; }
        /// <summary>
        /// 是否连接
        /// </summary>
        public abstract bool IsConnect { get; }
        /// <summary>
        /// 连接
        /// </summary>
        /// <returns></returns>
        public abstract bool Connect();
        /// <summary>
        /// 断开
        /// </summary>
        /// <returns></returns>
        public abstract bool DisConnect();

        /// <summary>
        /// 写入单个寄存器 功能码06
        /// </summary>
        /// <param name="slaveID">从站ID</param>
        /// <param name="startAddress">寄存器开始地址</param>
        /// <param name="value">写入数据</param>
        public abstract void WriteSingleRegister(int slaveID, ushort startAddress, ushort value);

        /// <summary>
        /// 写入多个寄存器 功能码10
        /// </summary>
        /// <param name="slaveID">从站ID</param>
        /// <param name="startAddress">寄存器开始地址</param>
        /// <param name="value">写入数据</param>
        public abstract void WriteMultipleRegisters(int slaveID, ushort startAddress, ushort[] value);

        /// <summary>
        /// 读取保持寄存器 功能码03
        /// </summary>
        /// <param name="slaveID">从站ID</param>
        /// <param name="startAddress">寄存器开始地址</param>
        /// <param name="length">寄存器长度</param>
        /// <returns></returns>
        public abstract byte[] ReadHoldingRegisters(int slaveID, ushort startAddress, ushort length);
    }
}
