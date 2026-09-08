using System.Buffers.Binary;
using System.Globalization;
using CommunicationNew.Protocols.Mitsubishi;

namespace CommunicationNew.MxComponentSmokeTest;

/// <summary>集中解析 MX 验收参数并执行所有写入安全校验。</summary>
internal sealed class SmokeOptions
{
    private bool _countSpecified;

    public int LogicalStationNumber { get; private set; }
    public string Address { get; private set; } = "D100";
    public McAddressValue AddressValue { get; private set; }
    public int Count { get; private set; } = 1;
    public string? BridgeExecutablePath { get; private set; }
    public int TimeoutMilliseconds { get; private set; } = 3000;
    public ushort[]? WordValues { get; private set; }
    public bool[]? BitValues { get; private set; }
    public bool ConfirmWrite { get; private set; }
    public bool ShowHelp { get; private set; }
    public bool HasWrite => WordValues is not null || BitValues is not null;
    public int Quantity => WordValues?.Length ?? BitValues?.Length ?? Count;

    public static SmokeOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var result = new SmokeOptions();
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--help" or "-h": result.ShowHelp = true; break;
                case "--confirm-write": result.ConfirmWrite = true; break;
                case "--station":
                    result.LogicalStationNumber = ParseInt(ReadValue(args, ref index), argument, 0, 1023);
                    break;
                case "--address": result.Address = ReadValue(args, ref index); break;
                case "--count":
                    result.Count = ParseInt(ReadValue(args, ref index), argument, 1, ushort.MaxValue);
                    result._countSpecified = true;
                    break;
                case "--bridge":
                    result.BridgeExecutablePath = Path.GetFullPath(ReadValue(args, ref index));
                    break;
                case "--timeout":
                    result.TimeoutMilliseconds = ParseInt(
                        ReadValue(args, ref index), argument, 1, int.MaxValue);
                    break;
                case "--write-words":
                    EnsureNoWrite(result);
                    result.WordValues = ParseWords(ReadValue(args, ref index));
                    break;
                case "--write-bits":
                    EnsureNoWrite(result);
                    result.BitValues = ParseBits(ReadValue(args, ref index));
                    break;
                default: throw new ArgumentException($"不支持的参数：{argument}");
            }
        }

        if (result.ShowHelp)
        {
            return result;
        }
        try
        {
            result.AddressValue = McAddressValue.Parse(result.Address);
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException)
        {
            throw new ArgumentException($"无效的 MC 地址：{result.Address}。", nameof(args), ex);
        }
        result.ValidateWriteIntent();
        result.ValidateRequestRange();
        return result;
    }

    /// <summary>生成旧 MC 字节写入 interface 所需的数据；仅在 HasWrite 为 true 时调用。</summary>
    public byte[] CreateWritePayload()
    {
        if (BitValues is not null)
        {
            return BitValues.Select(value => value ? (byte)1 : (byte)0).ToArray();
        }
        if (WordValues is null)
        {
            throw new InvalidOperationException("当前参数不是写入操作。");
        }

        var payload = new byte[WordValues.Length * 2];
        for (var index = 0; index < WordValues.Length; index++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(index * 2, 2), WordValues[index]);
        }
        return payload;
    }

    private void ValidateWriteIntent()
    {
        if (!HasWrite)
        {
            if (ConfirmWrite)
            {
                throw new ArgumentException("--confirm-write 必须与 --write-words 或 --write-bits 一起使用。");
            }
            return;
        }
        if (_countSpecified)
        {
            throw new ArgumentException("写入点数由写入值决定，不能同时指定 --count。");
        }
        if (!ConfirmWrite)
        {
            throw new ArgumentException("写入必须同时提供 --confirm-write，防止误改 PLC 数据。");
        }
        if (AddressValue.IsBit != (BitValues is not null))
        {
            throw new ArgumentException(
                AddressValue.IsBit
                    ? $"{AddressValue.Prefix} 是位设备，请使用 --write-bits。"
                    : $"{AddressValue.Prefix} 是字设备，请使用 --write-words。");
        }
    }

    /// <summary>在创建 MX COM 对象前复用 MC 请求约束，避免无效参数触发设备连接。</summary>
    private void ValidateRequestRange()
    {
        if (Quantity > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Quantity), "访问点数不能超过 65535。");
        }

        // 读写具有相同的单次点数和 24 位设备编号边界，读请求无需构造写入线格式负载。
        _ = McRequest.Read(AddressValue, (ushort)Quantity);
    }

    private static string ReadValue(string[] args, ref int index)
    {
        if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{args[index - 1]} 缺少参数值。");
        }
        return args[index];
    }

    private static int ParseInt(string text, string name, int min, int max)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
            value < min || value > max)
        {
            throw new ArgumentException($"{name} 必须在 {min} 到 {max} 之间。");
        }
        return value;
    }

    private static ushort[] ParseWords(string text)
    {
        try
        {
            var values = SplitValues(text)
                .Select(value => value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? ushort.Parse(value[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)
                    : ushort.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture))
                .ToArray();
            return values.Length == 0
                ? throw new ArgumentException("--write-words 至少需要一个值。")
                : values;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new ArgumentException(
                "--write-words 只接受 0 到 65535 的十进制值或 0x0000 到 0xFFFF。",
                ex);
        }
    }

    private static bool[] ParseBits(string text)
    {
        var values = SplitValues(text)
            .Select(value => value switch
            {
                "0" => false,
                "1" => true,
                _ => throw new ArgumentException("--write-bits 只接受以逗号分隔的 0 或 1。"),
            })
            .ToArray();
        return values.Length == 0
            ? throw new ArgumentException("--write-bits 至少需要一个值。")
            : values;
    }

    private static string[] SplitValues(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void EnsureNoWrite(SmokeOptions options)
    {
        if (options.HasWrite)
        {
            throw new ArgumentException("--write-words 与 --write-bits 不能同时使用或重复指定。");
        }
    }
}
