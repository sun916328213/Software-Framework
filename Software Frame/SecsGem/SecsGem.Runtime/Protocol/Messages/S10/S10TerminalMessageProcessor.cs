using System.Globalization;
using Secs4Net;

namespace SecsGem.Runtime.Order.S10;

/// <summary>S10F3/F5 共用的轻量显示转换；不把示例 SECS 类型当成唯一允许的输入类型。</summary>
internal static class S10TerminalMessageProcessor
{
    internal static Item? Process(string command, Item? body, SecsGemRuntime runtime)
    {
        try
        {
            if (body is not { Format: SecsFormat.List, Count: 2 }) throw new ArgumentException("正文需要终端标识和显示内容两项。");
            var id = ReadValues(body.Items[0]);
            if (id.Length != 1 || !byte.TryParse(id[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                throw new ArgumentException("TID 需要可转换为 0–255 整数的单个值。");
            return Item.B(runtime.EnqueueTerminalMessage(value, command, ReadText(body.Items[1])));
        }
        catch (ArgumentException ex)
        {
            runtime.RaiseErrorPublic($"{command} 数据无法转换：{ex.Message}");
            return null; // 不伪造终端编号或显示成功；由现有接收路径报告 S9F7。
        }
    }

    private static string ReadText(Item item) => item.Format == SecsFormat.List
        ? string.Join("\r\n", item.Items.Select(ReadText))
        : string.Join(" ", ReadValues(item));

    private static string[] ReadValues(Item item) => item.Format switch
    {
        SecsFormat.ASCII or SecsFormat.JIS8 => new[] { item.GetString() },
        SecsFormat.Binary => Values(item.GetMemory<byte>()),
        SecsFormat.U1 => Values(item.GetMemory<byte>()),
        SecsFormat.U2 => Values(item.GetMemory<ushort>()),
        SecsFormat.U4 => Values(item.GetMemory<uint>()),
        SecsFormat.U8 => Values(item.GetMemory<ulong>()),
        SecsFormat.I1 => Values(item.GetMemory<sbyte>()),
        SecsFormat.I2 => Values(item.GetMemory<short>()),
        SecsFormat.I4 => Values(item.GetMemory<int>()),
        SecsFormat.I8 => Values(item.GetMemory<long>()),
        SecsFormat.F4 => Values(item.GetMemory<float>()),
        SecsFormat.F8 => Values(item.GetMemory<double>()),
        SecsFormat.Boolean => Values(item.GetMemory<bool>()),
        _ => throw new ArgumentException($"{item.Format} 不能直接转换为显示值。")
    };

    private static string[] Values<T>(Memory<T> values) where T : unmanaged, IEquatable<T> => values.ToArray()
        .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
}
