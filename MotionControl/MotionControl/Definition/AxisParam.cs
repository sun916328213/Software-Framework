namespace MotionControl.Definition
{
    /// <summary>
    /// 单轴参数
    /// </summary>
    public class AxisParam
    {
        /// <summary>
        /// 轴名称
        /// </summary>
        public string Name { get; set; } = "Axis";
        /// <summary>
        /// 脉冲当量（脉冲数/用户单位，例如 1000 表示每毫米1000个脉冲）
        /// </summary>
        public double PulseEquivalent { get; set; } = 1000;
        /// <summary>
        /// 启动速度（用户单位/s）
        /// </summary>
        public double VelLow { get; set; } = 1;
        /// <summary>
        /// 运行速度（用户单位/s），可由SetSpeed运行时修改
        /// </summary>
        public double VelHigh { get; set; } = 100;
        /// <summary>
        /// 加速度（用户单位/s²）
        /// </summary>
        public double Accel { get; set; } = 1000;
        /// <summary>
        /// 减速度（用户单位/s²）
        /// </summary>
        public double Decel { get; set; } = 1000;
        /// <summary>
        /// 是否启用软限位
        /// </summary>
        public bool EnableSoftLimit { get; set; }
        /// <summary>
        /// 正软限位（用户单位）
        /// </summary>
        public double SoftLimitPositive { get; set; }
        /// <summary>
        /// 负软限位（用户单位）
        /// </summary>
        public double SoftLimitNegative { get; set; }
        /// <summary>
        /// 回零模式
        /// </summary>
        public EHomeMode HomeMode { get; set; } = EHomeMode.OriginSwitch;
        /// <summary>
        /// 回零方向
        /// </summary>
        public EDirection HomeDirection { get; set; } = EDirection.Negative;
        /// <summary>
        /// 回零速度（用户单位/s）
        /// </summary>
        public double HomeVelocity { get; set; } = 5;
        /// <summary>
        /// 回零偏移量（用户单位），回到原点后再偏移该距离作为零点
        /// </summary>
        public double HomeOffset { get; set; }

        /// <summary>
        /// 创建运行期快照。派生类型包含额外可变成员时应重写此方法并完成深拷贝。
        /// </summary>
        public virtual AxisParam Snapshot() => (AxisParam)MemberwiseClone();
    }
}
