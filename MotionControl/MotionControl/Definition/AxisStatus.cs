namespace MotionControl.Definition
{
    /// <summary>
    /// 轴状态标志位（厂商实现负责将SDK状态字映射到该枚举）
    /// </summary>
    [Flags]
    public enum EAxisStatusFlag
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,
        /// <summary>
        /// 伺服使能
        /// </summary>
        Enabled = 1 << 0,
        /// <summary>
        /// 运动中
        /// </summary>
        Moving = 1 << 1,
        /// <summary>
        /// 到位
        /// </summary>
        InPosition = 1 << 2,
        /// <summary>
        /// 报警
        /// </summary>
        Alarm = 1 << 3,
        /// <summary>
        /// 正限位触发
        /// </summary>
        PositiveLimit = 1 << 4,
        /// <summary>
        /// 负限位触发
        /// </summary>
        NegativeLimit = 1 << 5,
        /// <summary>
        /// 已回零
        /// </summary>
        Homed = 1 << 6,
        /// <summary>
        /// 急停触发
        /// </summary>
        EmergencyStop = 1 << 7,
    }

    /// <summary>
    /// 轴状态
    /// </summary>
    public class AxisStatus
    {
        /// <summary>
        /// 轴索引
        /// </summary>
        public int Axis { get; set; }
        /// <summary>
        /// 厂商原始状态字
        /// </summary>
        public long Raw { get; set; }
        /// <summary>
        /// 状态标志
        /// </summary>
        public EAxisStatusFlag Flags { get; set; }

        /// <summary>伺服使能</summary>
        public bool Enabled => Flags.HasFlag(EAxisStatusFlag.Enabled);
        /// <summary>运动中</summary>
        public bool Moving => Flags.HasFlag(EAxisStatusFlag.Moving);
        /// <summary>到位</summary>
        public bool InPosition => Flags.HasFlag(EAxisStatusFlag.InPosition);
        /// <summary>报警</summary>
        public bool Alarm => Flags.HasFlag(EAxisStatusFlag.Alarm);
        /// <summary>正限位</summary>
        public bool PositiveLimit => Flags.HasFlag(EAxisStatusFlag.PositiveLimit);
        /// <summary>负限位</summary>
        public bool NegativeLimit => Flags.HasFlag(EAxisStatusFlag.NegativeLimit);
        /// <summary>已回零</summary>
        public bool Homed => Flags.HasFlag(EAxisStatusFlag.Homed);
        /// <summary>急停</summary>
        public bool EmergencyStop => Flags.HasFlag(EAxisStatusFlag.EmergencyStop);

        /// <inheritdoc/>
        public override string ToString()
        {
            var parts = new List<string>();
            if (Enabled) parts.Add(nameof(Enabled));
            if (Moving) parts.Add(nameof(Moving));
            if (InPosition) parts.Add(nameof(InPosition));
            if (Alarm) parts.Add(nameof(Alarm));
            if (PositiveLimit) parts.Add(nameof(PositiveLimit));
            if (NegativeLimit) parts.Add(nameof(NegativeLimit));
            if (Homed) parts.Add(nameof(Homed));
            if (EmergencyStop) parts.Add(nameof(EmergencyStop));
            return $"Axis{Axis}: [{string.Join(",", parts)}] Raw=0x{Raw:X}";
        }
    }

    /// <summary>
    /// 轴报警事件参数
    /// </summary>
    public class AxisAlarmEventArgs : EventArgs
    {
        /// <summary>
        /// 轴索引
        /// </summary>
        public int Axis { get; }
        /// <summary>
        /// 报警信息
        /// </summary>
        public string Message { get; }
        /// <summary>
        /// 报警时的原始状态字
        /// </summary>
        public long RawStatus { get; }
        /// <summary>
        /// 报警时间
        /// </summary>
        public DateTime Time { get; } = DateTime.Now;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="message">报警信息</param>
        /// <param name="rawStatus">原始状态字</param>
        public AxisAlarmEventArgs(int axis, string message, long rawStatus)
        {
            Axis = axis;
            Message = message;
            RawStatus = rawStatus;
        }
    }
}
