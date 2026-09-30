using Secs.Manager;
using Secs.Variable;
using SecsGem.Core;

namespace SecsGem.Runtime.DataBinding;

/// <summary>VID 存储与设备数据快照的唯一协调入口。</summary>
internal sealed class SecsDataBinding
{
    private readonly VIDManager _manager;
    private IEquipmentDataProvider? _provider;

    internal SecsDataBinding(VIDManager manager)
    {
        _manager = manager;
    }

    internal VIDManager Manager => _manager;

    internal void SetProvider(IEquipmentDataProvider? provider) => _provider = provider;

    internal void SetVariable(string name, object value) => _manager.UpdateVID(name, value);

    internal void AppendVariable(string name, object value) => _manager.AppendVID(name, value);

    internal VIDInfo GetVariable(string name) => _manager.GetVIDByName(name);

    internal string? GetVariableValue(string name) => _manager.GetValueByName(name);

    internal void Refresh()
    {
        var provider = _provider;
        if (provider == null) return;

        var values = provider.GetVariableValues()
            ?? throw new InvalidOperationException("Equipment data provider returned no variable snapshot.");
        foreach (var (name, value) in values)
        {
            if (_manager.VIDDic_Name.TryGetValue(name, out var vid))
            {
                vid.Value = value!;
            }
        }
    }
}
