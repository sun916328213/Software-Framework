using Secs4Net;
using System.Collections;

namespace SecsGem.Runtime.Order.S7;

/// <summary>S7 报文与配方数据的共用转换、结构读取和深复制；不校验具体配方业务内容。</summary>
internal static class ProcessProgramCodec
{
    private static string Describe(Item? item) => item is null ? "无正文" : $"{item.Format}[{item.Count}]";

    internal static string ReadId(Item? item)
    {
        if (item?.Format != SecsFormat.ASCII) throw new ArgumentException($"PPID 要求 ASCII，实际 {Describe(item)}");
        var id = item.GetString();
        // PPID 保留已有 ASCII 字符串身份约定，避免数字转换、前导零丢失及 S9 会话标识不一致。
        // 字符数量上限由项目接收策略决定，不在共用转换器中固定为 120。
        if (id.Length == 0 || id.Any(c => c > 127))
            throw new ArgumentException("PPID 必须为非空 ASCII 字符串");
        return id;
    }

    internal static void RequireList(Item? item, int count, string path = "正文")
    {
        if (item?.Format != SecsFormat.List || item.Count != count)
            throw new ArgumentException($"{path} 要求 L[{count}]，实际 {Describe(item)}");
    }

    internal static ProcessProgramData ReadFormatted(Item? item)
    {
        var fields = ReadFormattedFields(item);
        return new ProcessProgramData
        {
            PPID = fields[0].GetString(), MDLN = fields[1].GetString(), SOFTREV = fields[2].GetString(),
            MetadataFormats = (fields[1].Format, fields[2].Format),
            PPBODY = fields[3].Items.Select(x => (object)Copy(x)).ToList(),
            ReceivedTime = DateTime.Now
        };
    }

    private static Item[] ReadFormattedFields(Item? item)
    {
        RequireList(item, 4);
        var fields = item!.Items;
        ReadId(fields[0]);
        for (var i = 1; i <= 2; i++)
        {
            var name = i == 1 ? "MDLN" : "SOFTREV";
            if (fields[i].Format is not (SecsFormat.ASCII or SecsFormat.JIS8))
                throw new ArgumentException($"{name} 需要文本项（ASCII/JIS8），实际 {Describe(fields[i])}");
        }
        if (fields[3].Format != SecsFormat.List)
            throw new ArgumentException($"命令列表要求 L[n]，实际 {Describe(fields[3])}");
        for (var i = 0; i < fields[3].Items.Length; i++)
        {
            var command = fields[3].Items[i];
            var path = $"命令[{i + 1}]";
            RequireList(command, 2, path);
            // CCODE 作为不透明 Item 原样保存/回传，格式和含义交给宿主业务编解码器。
            if (command.Items[1].Format != SecsFormat.List)
                throw new ArgumentException($"{path}.PPARM 要求 L[n]，实际 {Describe(command.Items[1])}");
        }
        return fields;
    }

    internal static Item WriteFormatted(ProcessProgramData data)
    {
        if (data.PPBODY == null) throw new ArgumentException("缺少命令列表");
        var item = Item.L(Item.A(data.PPID), Text(data.MDLN, data.MetadataFormats.Model),
            Text(data.SOFTREV, data.MetadataFormats.Revision), Item.L(data.PPBODY.Select(Value)));
        try
        {
            ReadFormattedFields(item); // 只检查能否按现有数据模型读取，不判断机型、版本或参数业务含义。
            return item;
        }
        catch
        {
            item.Dispose(); // 失败时释放本次创建的 Item 树，不释放调用方的数据。
            throw;
        }
    }

    private static Item Text(string value, SecsFormat format) => format == SecsFormat.JIS8 ? Item.J(value) : Item.A(value);

    internal static Item Value(object value) => value switch
    {
        Item item => Copy(item),
        string s => Item.A(s),
        byte b => Item.U1(b), byte[] a => Item.B(a.ToArray()),
        sbyte x => Item.I1(x), sbyte[] a => Item.I1(a.ToArray()),
        short x => Item.I2(x), short[] a => Item.I2(a.ToArray()),
        int x => Item.I4(x), int[] a => Item.I4(a.ToArray()),
        long x => Item.I8(x), long[] a => Item.I8(a.ToArray()),
        ushort x => Item.U2(x), ushort[] a => Item.U2(a.ToArray()),
        uint x => Item.U4(x), uint[] a => Item.U4(a.ToArray()),
        ulong x => Item.U8(x), ulong[] a => Item.U8(a.ToArray()),
        bool x => Item.Boolean(x), bool[] a => Item.Boolean(a.ToArray()),
        float x => Item.F4(x), float[] a => Item.F4(a.ToArray()),
        double x => Item.F8(x), double[] a => Item.F8(a.ToArray()),
        IEnumerable values => Item.L(values.Cast<object>().Select(Value)),
        _ => throw new ArgumentException("不支持的配方参数类型")
    };

    // 不能保留传输层 Item 的借用内存，也不能将数值向量截成第一个元素。
    internal static Item Copy(Item item) => item.Format switch
    {
        SecsFormat.List => Item.L(item.Items.Select(Copy)),
        SecsFormat.ASCII => Item.A(item.GetString()),
        SecsFormat.JIS8 => Item.J(item.GetString()),
        SecsFormat.Binary => Item.B(item.GetMemory<byte>().ToArray()),
        SecsFormat.U1 => Item.U1(item.GetMemory<byte>().ToArray()),
        SecsFormat.U2 => Item.U2(item.GetMemory<ushort>().ToArray()),
        SecsFormat.U4 => Item.U4(item.GetMemory<uint>().ToArray()),
        SecsFormat.U8 => Item.U8(item.GetMemory<ulong>().ToArray()),
        SecsFormat.I1 => Item.I1(item.GetMemory<sbyte>().ToArray()),
        SecsFormat.I2 => Item.I2(item.GetMemory<short>().ToArray()),
        SecsFormat.I4 => Item.I4(item.GetMemory<int>().ToArray()),
        SecsFormat.I8 => Item.I8(item.GetMemory<long>().ToArray()),
        SecsFormat.F4 => Item.F4(item.GetMemory<float>().ToArray()),
        SecsFormat.F8 => Item.F8(item.GetMemory<double>().ToArray()),
        SecsFormat.Boolean => Item.Boolean(item.GetMemory<bool>().ToArray()),
        _ => throw new ArgumentException("不支持的 SECS 数据格式")
    };
}
