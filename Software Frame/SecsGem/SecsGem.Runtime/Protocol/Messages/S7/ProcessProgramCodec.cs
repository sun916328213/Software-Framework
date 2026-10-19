using Secs4Net;
using System.Collections;

namespace SecsGem.Runtime.Order.S7;

/// <summary>SECS-II S7 的 ASCII PPID 配置：1–120 字符；不隐式转换或截断。</summary>
internal static class ProcessProgramCodec
{
    private static string Describe(Item? item) => item is null ? "无正文" : $"{item.Format}[{item.Count}]";

    internal static string ReadId(Item? item)
    {
        if (item?.Format != SecsFormat.ASCII) throw new ArgumentException($"PPID 要求 ASCII，实际 {Describe(item)}");
        var id = item.GetString();
        if (id.Length is < 1 or > 120 || id.Any(c => c > 127))
            throw new ArgumentException("PPID 必须为 1–120 个 ASCII 字符");
        return id;
    }

    internal static void RequireList(Item? item, int count, string path = "正文")
    {
        if (item?.Format != SecsFormat.List || item.Count != count)
            throw new ArgumentException($"{path} 要求 L[{count}]，实际 {Describe(item)}");
    }

    internal static ProcessProgramData ReadFormatted(Item? item)
    {
        ValidateFormatted(item);
        var fields = item!.Items;
        return new ProcessProgramData
        {
            PPID = fields[0].GetString(), MDLN = fields[1].GetString(), SOFTREV = fields[2].GetString(),
            PPBODY = fields[3].Items.Select(x => (object)Copy(x)).ToList(),
            ReceivedTime = DateTime.Now
        };
    }

    private static void ValidateFormatted(Item? item)
    {
        RequireList(item, 4);
        var fields = item!.Items;
        ReadId(fields[0]);
        for (var i = 1; i <= 2; i++)
        {
            var name = i == 1 ? "MDLN" : "SOFTREV";
            if (fields[i].Format != SecsFormat.ASCII)
                throw new ArgumentException($"{name} 要求 ASCII，实际 {Describe(fields[i])}");
            var text = fields[i].GetString();
            if (text.Length > 20 || text.Any(c => c > 127))
                throw new ArgumentException($"{name} 必须为最多 20 个 ASCII 字符，实际长度 {text.Length}");
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
    }

    internal static Item WriteFormatted(ProcessProgramData data)
    {
        if (data.PPBODY == null) throw new ArgumentException("缺少命令列表");
        var item = Item.L(Item.A(data.PPID), Item.A(data.MDLN), Item.A(data.SOFTREV),
            Item.L(data.PPBODY.Select(Value)));
        // 同一验证器保护发包与收包；旧的 L[2] 私有结构不再发送。
        ValidateFormatted(item);
        return item;
    }

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
