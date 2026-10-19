using System.Reflection;
using Secs4Net;
using SecsGem.Runtime.Order.S7;

namespace SecsGem.Controls.Services;

/// <summary>宿主可替换的格式化配方编解码；不连接设备、不选择配方，返回自有数据。</summary>
public interface IFormattedRecipeCodec<T> where T : notnull
{
    ProcessProgramData Encode(string ppid, T recipe);
    T Decode(ProcessProgramData program);
}

/// <summary>可选的简单参数块实现；复杂命令、多步骤或其他数据类型由宿主实现接口。</summary>
public sealed class ParameterBlockRecipeCodec<T> : IFormattedRecipeCodec<T> where T : notnull
{
    private readonly string[] _parameters;
    private readonly ushort _commandCode;
    private readonly string _model;
    private readonly string _softwareRevision;

    public ParameterBlockRecipeCodec(IReadOnlyList<string> parameters, ushort commandCode = 1,
        string model = "", string softwareRevision = "")
    {
        ArgumentNullException.ThrowIfNull(parameters);
        _parameters = parameters.ToArray();
        if (_parameters.Length == 0 || _parameters.Distinct(StringComparer.Ordinal).Count() != _parameters.Length)
            throw new ArgumentException("简单参数块必须提供非空且不重复的属性列表。", nameof(parameters));
        _commandCode = commandCode;
        _model = model;
        _softwareRevision = softwareRevision;
    }

    public T Decode(ProcessProgramData program) =>
        ProcessProgramCommunication.DeserializeFormatted<T>(program, _parameters, _commandCode);

    public ProcessProgramData Encode(string ppid, T recipe)
    {
        ProcessProgramCommunication.ValidatePpid(ppid);
        ProcessProgramCommunication.Serialize(recipe); // 与二进制路径采用相同的宿主必填/范围校验。
        var parameters = _parameters.Select(name =>
        {
            var property = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException($"宿主配方没有属性 {name}。");
            return property.GetValue(recipe) switch
            {
                int value when value >= 0 => Item.U4(checked((uint)value)),
                uint value => Item.U4(value),
                double value when double.IsFinite(value) => Item.F8(value),
                _ => throw new ArgumentException($"属性 {name} 需要非负 int/uint（U4）或有限 double（F8）；其他映射请提供自定义编解码器。")
            };
        }).ToArray();
        return new ProcessProgramData
        {
            PPID = ppid, MDLN = _model, SOFTREV = _softwareRevision,
            PPBODY = new() { Item.L(Item.U2(_commandCode), Item.L(parameters)) }
        };
    }
}
