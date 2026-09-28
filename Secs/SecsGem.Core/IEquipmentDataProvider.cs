namespace SecsGem.Core;

/// <summary>由设备宿主提供当前变量值；键为已配置的 VID 名称。</summary>
public interface IEquipmentDataProvider
{
    IReadOnlyDictionary<string, object?> GetVariableValues();
}
