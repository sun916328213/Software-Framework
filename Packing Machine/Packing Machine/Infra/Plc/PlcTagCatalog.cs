using System.IO;
using System.Text.RegularExpressions;

namespace Packing_Machine.Infra.Plc;

/// <summary>
/// PLCIO.csv 点位目录：只负责解析、校验、按名称索引和 base+offset 地址解析，不负责通信。
/// <para>
/// 寄存器单位（冻结旧语义）：所有 offset / length 均按 16-bit 寄存器点数，1 点 = 1 个 short。
/// 绝对地址 = 基础 D 寄存器号 + 寄存器偏移，不按字节相加。
/// </para>
/// </summary>
public sealed class PlcTagCatalog
{
    private static readonly Regex DAddressPattern = new(@"^D(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly Dictionary<string, PlcTagDefinition> _tags;

    /// <summary>解析点位表；重复名、非法地址、未知类型将启动即失败。</summary>
    /// <exception cref="FileNotFoundException">点位表文件不存在。</exception>
    /// <exception cref="InvalidOperationException">点位表内容非法。</exception>
    public PlcTagCatalog(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"PLC 点位表不存在：{csvPath}。", csvPath);
        }

        _tags = new Dictionary<string, PlcTagDefinition>(StringComparer.Ordinal);
        var lineNumber = 0;
        foreach (var rawLine in File.ReadLines(csvPath))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var fields = rawLine.Split(',');
            if (fields.Length < 3)
            {
                throw new InvalidOperationException($"PLCIO.csv 第 {lineNumber} 行字段不足（需要 Name,Address,Type）：{rawLine}。");
            }

            // 首行表头（Name,Address,Type,Length）跳过，不计为点位。
            if (lineNumber == 1 && fields[0].Trim().Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = fields[0].Trim();
            var address = fields[1].Trim();
            var typeText = fields[2].Trim();
            var lengthText = fields.Length > 3 ? fields[3].Trim() : string.Empty;

            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException($"PLCIO.csv 第 {lineNumber} 行名称为空。");
            }

            if (_tags.ContainsKey(name))
            {
                throw new InvalidOperationException($"PLCIO.csv 存在重复点位名：{name}（第 {lineNumber} 行）。");
            }

            var match = DAddressPattern.Match(address);
            if (!match.Success)
            {
                throw new InvalidOperationException(
                    $"PLCIO.csv 第 {lineNumber} 行地址非法：'{address}'。P5 只接受 D 字寄存器（如 D8000）。");
            }

            if (!Enum.TryParse<PlcTagType>(typeText, ignoreCase: true, out var type))
            {
                throw new InvalidOperationException(
                    $"PLCIO.csv 第 {lineNumber} 行类型未知：'{typeText}'。只支持 Short/Int/Float/Double/String。");
            }

            int? registerLength = null;
            if (type == PlcTagType.String)
            {
                if (string.IsNullOrEmpty(lengthText))
                {
                    // 偏离 P5 计划 fail-fast 的一点：FOSB_Write（String、无 Length）正被 ReadID 站点
                    // 用于写入 FOSB 序列号。启动即失败会直接破坏现有生产调用，因此允许无 Length 的
                    // String 作为“动态长度”（写入按值长度、偶数字节对齐；读取需显式长度）。
                    registerLength = null;
                }
                else if (!int.TryParse(lengthText, out var parsed) || parsed <= 0)
                {
                    throw new InvalidOperationException(
                        $"PLCIO.csv 第 {lineNumber} 行 String 长度非法：'{lengthText}'（应为正整数寄存器数）。");
                }
                else
                {
                    registerLength = parsed;
                }
            }

            _tags.Add(name, new PlcTagDefinition(
                name,
                $"D{int.Parse(match.Groups[1].Value)}",
                int.Parse(match.Groups[1].Value),
                type,
                registerLength));
        }

        if (_tags.Count == 0)
        {
            throw new InvalidOperationException($"PLCIO.csv 未解析到任何有效点位：{csvPath}。");
        }
    }

    /// <summary>点位数量。</summary>
    public int Count => _tags.Count;

    /// <summary>按名称取点位定义；缺失时抛错（调用时失败，不在启动时预判业务使用面）。</summary>
    public PlcTagDefinition Resolve(string name)
    {
        if (!_tags.TryGetValue(name, out var tag))
        {
            throw new KeyNotFoundException(
                $"PLC 点位表无此点位：'{name}'。如为新增业务点位请先维护 Config/PLCIO.csv。");
        }

        return tag;
    }

    /// <summary>解析绝对 D 地址：基础寄存器号 + 寄存器偏移。</summary>
    public string ResolveAddress(string name, int offset)
    {
        var tag = Resolve(name);
        var absolute = checked(tag.BaseNumber + offset);
        if (absolute < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), $"点位 {name} 偏移 {offset} 后地址为负。");
        }

        return $"D{absolute}";
    }

    /// <summary>点位固有寄存器数量；String 动态长度返回 null（由调用方按值确定）。</summary>
    public static int? GetRegisterCount(PlcTagDefinition tag) =>
        tag.Type switch
        {
            PlcTagType.Short => 1,
            PlcTagType.Int => 2,
            PlcTagType.Float => 2,
            PlcTagType.Double => 4,
            PlcTagType.String => tag.RegisterLength,
            _ => throw new ArgumentOutOfRangeException(nameof(tag)),
        };
}
