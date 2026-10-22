using Secs4Net;

namespace SecsGem.Runtime.Protocol.Parsing;

/// <summary>旧字符串列表接口；只取受支持数值项的首元素，不替代配方的原类型深复制或 S10 的向量显示转换。</summary>
internal static class SecsItemParser
{
    internal static List<string>? Flatten(Item? data)
    {
        if (data is null) return null;

        var strings = new List<string>();
        var items = data.Format == SecsFormat.List ? data.Items : new[] { data };
        foreach (var item in items)
        {
            if (item.Format == SecsFormat.List)
            {
                strings.AddRange(Flatten(item)!); // item 非空，递归结果不会走 null 输入分支。
                continue;
            }

            var value = item.Format switch
            {
                SecsFormat.U1 or SecsFormat.Binary => ReadFirstValue<byte>(item),
                SecsFormat.U2 => ReadFirstValue<ushort>(item),
                SecsFormat.U4 => ReadFirstValue<uint>(item),
                SecsFormat.U8 => ReadFirstValue<ulong>(item),
                SecsFormat.ASCII => item.GetString(),
                SecsFormat.Boolean => ReadFirstValue<bool>(item),
                _ => null
            };
            if (value is not null) strings.Add(value);
        }
        return strings;
    }

    internal static string? ReadFirstValue<T>(Item item) where T : unmanaged, IEquatable<T>
    {
        var values = item.GetMemory<T>();
        return values.IsEmpty ? null : values.Span[0].ToString();
    }
}
