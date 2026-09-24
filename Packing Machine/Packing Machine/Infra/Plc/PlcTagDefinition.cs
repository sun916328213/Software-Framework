namespace Packing_Machine.Infra.Plc;

/// <summary>PLC 点位数据类型。寄存器数量语义见 <see cref="PlcTagCatalog"/>。</summary>
public enum PlcTagType
{
    /// <summary>1 个 D 寄存器（2 字节）。</summary>
    Short,
    /// <summary>连续 2 个 D 寄存器（4 字节）。</summary>
    Int,
    /// <summary>连续 2 个 D 寄存器（4 字节浮点）。</summary>
    Float,
    /// <summary>连续 4 个 D 寄存器（8 字节浮点）。当前 CSV 暂无，仅保留支持能力。</summary>
    Double,
    /// <summary>固定或动态长度的字符串；字节数 = 寄存器数 × 2。</summary>
    String,
}

/// <summary>PLCIO.csv 中的单个点位定义（解析后不可变）。</summary>
public sealed class PlcTagDefinition
{
    public PlcTagDefinition(string name, string address, int baseNumber, PlcTagType type, int? registerLength)
    {
        Name = name;
        Address = address;
        BaseNumber = baseNumber;
        Type = type;
        RegisterLength = registerLength;
    }

    /// <summary>点位名称（CSV Name 列，Trim 后）。</summary>
    public string Name { get; }

    /// <summary>基础 D 地址原文（如 D8250）。</summary>
    public string Address { get; }

    /// <summary>基础 D 寄存器号。</summary>
    public int BaseNumber { get; }

    /// <summary>点位类型。</summary>
    public PlcTagType Type { get; }

    /// <summary>
    /// String 点位的寄存器长度；null 表示动态长度（仅允许写入时按值长度，
    /// 读取需显式长度）。非 String 类型恒为 null。
    /// </summary>
    public int? RegisterLength { get; }
}
