using MotionControl.Definition;

namespace MotionControl.Vendors.Inovance
{
    /// <summary>
    /// 汇川控制参数：在通用卡参数基础上增加Modbus TCP连接与寄存器映射配置
    /// </summary>
    public class InovanceParam : MotionCardParam
    {
        /// <summary>
        /// Modbus从站号（汇川默认1）
        /// </summary>
        public byte SlaveId { get; set; } = 1;
        /// <summary>
        /// 各轴控制块基地址（对应PLC侧HD寄存器地址，须与AutoShop程序中的接口FB布局一致）
        /// </summary>
        public ushort RegisterBase { get; set; } = 100;
        /// <summary>
        /// 相邻轴控制块的寄存器跨度（默认20：控制字1+状态字1+目标位置2+目标速度2+当前位置2+保留12）
        /// </summary>
        public ushort RegisterStride { get; set; } = 20;

        /// <summary>指定轴的控制字地址</summary>
        public ushort ControlWordAddress(int axis) => (ushort)(RegisterBase + axis * RegisterStride);
        /// <summary>指定轴的状态字地址</summary>
        public ushort StatusWordAddress(int axis) => (ushort)(RegisterBase + axis * RegisterStride + 1);
        /// <summary>指定轴的目标位置地址（DINT，占2个寄存器）</summary>
        public ushort TargetPositionAddress(int axis) => (ushort)(RegisterBase + axis * RegisterStride + 2);
        /// <summary>指定轴的目标速度地址（REAL，占2个寄存器）</summary>
        public ushort TargetVelocityAddress(int axis) => (ushort)(RegisterBase + axis * RegisterStride + 4);
        /// <summary>指定轴的当前位置地址（DINT，占2个寄存器）</summary>
        public ushort CurrentPositionAddress(int axis) => (ushort)(RegisterBase + axis * RegisterStride + 6);
    }
}
