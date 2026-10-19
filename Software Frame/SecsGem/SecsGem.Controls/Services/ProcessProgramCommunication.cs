using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Reflection;
using SecsGem.Runtime.Order.S7;
using System.Text.Json.Serialization;
using Secs4Net;
using Secs4Net.Sml;

namespace SecsGem.Controls.Services;

/// <summary>通用 JSON 配方转换和 S7 请求组包；不依赖宿主的具体配方类型。</summary>
public static class ProcessProgramCommunication
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static readonly JsonSerializerOptions FormattedJsonOptions = new(JsonOptions)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static byte[] Serialize<T>(T recipe) where T : notnull
    {
        // 结构体只装箱一次；Validator 要求 instance 与 Context.ObjectInstance 引用相同。
        object instance = recipe;
        Validator.ValidateObject(instance, new ValidationContext(instance), true);
        return JsonSerializer.SerializeToUtf8Bytes(recipe, JsonOptions);
    }

    public static T Deserialize<T>(ReadOnlySpan<byte> body) where T : notnull => Deserialize<T>(body, JsonOptions);

    private static T Deserialize<T>(ReadOnlySpan<byte> body, JsonSerializerOptions options) where T : notnull
    {
        var recipe = JsonSerializer.Deserialize<T>(body, options);
        if (recipe is null) throw new JsonException("配方正文不能为 null。");
        object instance = recipe;
        Validator.ValidateObject(instance, new ValidationContext(instance), true);
        return recipe;
    }

    /// <summary>按宿主显式指定的属性顺序转换单个参数块；不猜测属性顺序或命令含义。</summary>
    public static T DeserializeFormatted<T>(ProcessProgramData program, IReadOnlyList<string> parameterNames,
        ushort commandCode = 1) where T : notnull
    {
        if (program.PPBODY.Count != 1 || program.PPBODY[0] is not Item { Format: SecsFormat.List, Count: 2 } command)
            throw new ArgumentException("业务配方要求恰好一个 L[2] 参数块命令。");
        var codeValue = ReadFormattedValue(command.Items[0], false);
        var code = JsonSerializer.Deserialize<ushort>(JsonSerializer.SerializeToUtf8Bytes(codeValue), FormattedJsonOptions);
        if (code != commandCode)
            throw new ArgumentException($"该业务参数块不识别 CCODE={code}，要求命令编号 {commandCode}。");
        var parameters = command.Items[1];
        if (parameters.Format != SecsFormat.List || parameters.Count != parameterNames.Count)
            throw new ArgumentException($"CCODE={commandCode} 的 PPARM 要求 L[{parameterNames.Count}]，实际 {parameters.Format}[{parameters.Count}]。");
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        for (var i = 0; i < parameterNames.Count; i++)
        {
            var property = typeof(T).GetProperty(parameterNames[i], BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException($"宿主配方没有属性 {parameterNames[i]}。");
            var collection = property.PropertyType != typeof(string)
                && typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType);
            var value = ReadFormattedValue(parameters.Items[i], collection);
            var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? JsonOptions.PropertyNamingPolicy!.ConvertName(property.Name);
            if (!values.TryAdd(jsonName, value)) throw new ArgumentException($"重复的参数映射：{property.Name}。");
        }
        // 复用宿主的必填、数值范围及结构体单次装箱校验；不截断数值或填默认参数。
        return Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(values, JsonOptions), FormattedJsonOptions);
    }

    // 原始 SECS 类型不改写；只在业务投影时读取值，由目标属性决定是否可转换。
    private static object ReadFormattedValue(Item item, bool collection)
    {
        if (item.Format is SecsFormat.ASCII or SecsFormat.JIS8) return item.GetString();
        if (item.Format == SecsFormat.List)
            return item.Items.Select(child => ReadFormattedValue(child, child.Format == SecsFormat.List)).ToArray();
        Array values = item.Format switch
        {
            SecsFormat.Binary or SecsFormat.U1 => item.GetMemory<byte>().ToArray(),
            SecsFormat.U2 => item.GetMemory<ushort>().ToArray(),
            SecsFormat.U4 => item.GetMemory<uint>().ToArray(),
            SecsFormat.U8 => item.GetMemory<ulong>().ToArray(),
            SecsFormat.I1 => item.GetMemory<sbyte>().ToArray(),
            SecsFormat.I2 => item.GetMemory<short>().ToArray(),
            SecsFormat.I4 => item.GetMemory<int>().ToArray(),
            SecsFormat.I8 => item.GetMemory<long>().ToArray(),
            SecsFormat.F4 => item.GetMemory<float>().ToArray(),
            SecsFormat.F8 => item.GetMemory<double>().ToArray(),
            SecsFormat.Boolean => item.GetMemory<bool>().ToArray(),
            _ => throw new ArgumentException($"无法读取 SECS 值：{item.Format}。")
        };
        if (collection) return values;
        if (values.Length != 1)
            throw new ArgumentException($"业务标量不能丢弃元素：收到 {item.Format}[{item.Count}]。");
        return values.GetValue(0)!;
    }

    public static void ValidatePpid(string ppid)
    {
        if (string.IsNullOrEmpty(ppid) || ppid.Any(c => c > 127))
            throw new ArgumentException("PPID 必须为非空 ASCII 字符串，数字编号也按文本编码。");
    }

    /// <summary>只生成 H→E 模板，不发送、不删除；格式化正文由通信规格决定，不猜测业务字段映射。</summary>
    public static SecsMessage CreateRequest(int index, string ppid, byte[]? binary = null, string? formattedSml = null)
    {
        if (index is not (4 or 7)) ValidatePpid(ppid);
        if (index is 0 or 1 && binary is null) throw new ArgumentException("没有该 PPID 的二进制正文，请先导入宿主配方。");
        if (index == 5)
        {
            if (string.IsNullOrWhiteSpace(formattedSml)) throw new ArgumentException("请按通信规格输入完整 S7F23 SML，通用页面不推测工艺命令映射。");
            var message = formattedSml.ToSecsMessage();
            if (message.S == 7 && message.F == 23 && message.ReplyExpected
                && message.SecsItem is { Format: SecsFormat.List, Count: 4 } body
                && body.Items[0].Format == SecsFormat.ASCII && body.Items[0].GetString() == ppid)
                return message;
            message.Dispose();
            throw new ArgumentException("格式化模板必须为 S7F23 W、L[4]，且 PPID 与测试输入一致。");
        }
        (int function, Item? item) = index switch
        {
            0 => (1, Item.L(Item.A(ppid), Item.U4((uint)binary!.Length))),
            1 => (3, Item.L(Item.A(ppid), Item.B(binary!.ToArray()))),
            2 => (5, Item.A(ppid)),
            3 => (17, Item.L(Item.A(ppid))),
            4 => (19, null),
            6 => (25, Item.A(ppid)),
            7 => (17, Item.L()),
            _ => throw new ArgumentException("请选择 S7 事务。")
        };
        return new SecsMessage(7, (byte)function, true) { SecsItem = item };
    }
}
