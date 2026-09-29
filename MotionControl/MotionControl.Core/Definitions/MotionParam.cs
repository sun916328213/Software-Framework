namespace MotionControl.Core.Definitions;

/// <summary>
/// 运动控制卡类型
/// <para>P1 从旧 <c>MotionControl.Definition.EMotionCardType</c> 迁移。</para>
/// <para>仅为兼容已有配置的标识枚举，不引用任何具体厂商 SDK 类型；第三方驱动优先使用 <see cref="MotionDriverId"/> 字符串标识。</para>
/// </summary>
public enum EMotionCardType
{
    /// <summary>
    /// 仿真卡（无硬件调试用）
    /// </summary>
    Simulation = 0,
    /// <summary>
    /// 凌华ADLINK轴卡（APS函数库，支持PCI-8154/8144/8254、AMP、EtherCAT等系列）
    /// </summary>
    ADLINK = 1,
    /// <summary>
    /// 雷赛Leadshine控制卡（LTDMC函数库，支持DMC3000等脉冲系列）
    /// </summary>
    Leadshine = 2,
    /// <summary>
    /// 固高Googol控制卡（gts.dll函数库，GTS系列）
    /// </summary>
    Googol = 3,
    /// <summary>
    /// 正运动ZMotion控制器/控制卡（zauxdll.dll函数库）
    /// </summary>
    ZMotion = 4,
    /// <summary>
    /// 汇川Inovance（Modbus TCP寄存器映射方式，适配H3U/H5U/Easy/AM系列PLC及IMC控制卡的网口）
    /// </summary>
    Inovance = 5,
}

/// <summary>
/// 运动控制卡参数
/// <para>P1 从旧 <c>MotionControl.Definition.MotionCardParam</c> 迁移，语义不变。</para>
/// </summary>
public class MotionCardParam
{
    /// <summary>
    /// 卡名称
    /// </summary>
    public string Name { get; set; } = "MotionCard";
    /// <summary>
    /// 卡号（板卡索引导，从0开始）
    /// </summary>
    public int CardNo { get; set; }
    /// <summary>
    /// 控制卡类型
    /// </summary>
    public EMotionCardType CardType { get; set; } = EMotionCardType.Simulation;
    /// <summary>
    /// 可扩展驱动标识。为空时继续使用 <see cref="CardType"/>，从而兼容现有配置。
    /// 第三方驱动应设置稳定的自定义标识，而无需扩展 <see cref="EMotionCardType"/>。
    /// </summary>
    public string? DriverId { get; set; }
    /// <summary>
    /// 厂商配置文件路径（凌华：MotionCreatorPro2导出的XML；固高：gts.cfg配置文件；为空则使用板卡默认配置）
    /// </summary>
    public string? ConfigFile { get; set; }
    /// <summary>
    /// 设备IP地址（网络型控制器：正运动ZAux_OpenEth、汇川Modbus TCP）
    /// </summary>
    public string? IpAddress { get; set; }
    /// <summary>
    /// 设备TCP端口（正运动默认502，汇川Modbus TCP默认502）
    /// </summary>
    public int TcpPort { get; set; } = 502;
    /// <summary>
    /// 各轴参数（列表顺序即轴索引顺序，从0开始）
    /// </summary>
    public List<AxisParam> Axises { get; set; } = new List<AxisParam>();

    /// <summary>解析当前配置最终使用的驱动标识。</summary>
    public MotionDriverId ResolveDriverId()
        => string.IsNullOrWhiteSpace(DriverId)
            ? MotionDriverId.From(CardType)
            : new MotionDriverId(DriverId);

    /// <summary>
    /// 创建连接期间使用的独立参数快照。
    /// 派生类型包含额外可变引用成员时应重写此方法并完成深拷贝。
    /// </summary>
    public virtual MotionCardParam Snapshot()
    {
        var snapshot = (MotionCardParam)MemberwiseClone();
        snapshot.Axises = Axises == null
            ? null!
            : Axises.Select(axis => axis?.Snapshot()!).ToList();
        return snapshot;
    }
}
