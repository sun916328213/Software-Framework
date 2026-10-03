// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsDataBinding。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Runtime、SecsGem.Runtime.Manager、SecsGem.Runtime.Variable、SecsGem.Core。导入不代表所有类型都会在运行时加载。
using SecsGem.Runtime;
using SecsGem.Runtime.Manager;
using SecsGem.Runtime.Variable;
using SecsGem.Core;

namespace SecsGem.Runtime.DataBinding;

/// <summary>VID 存储与设备数据快照的唯一协调入口。</summary>
internal sealed class SecsDataBinding
{
    private readonly VIDManager _manager;
    private IEquipmentDataProvider? _provider;

    /// <summary>
    /// 初始化 SecsDataBinding 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    internal SecsDataBinding(VIDManager manager)
    {
        _manager = manager;
    }

    internal VIDManager Manager => _manager;

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetProvider。
    /// </summary>
    internal void SetProvider(IEquipmentDataProvider? provider) => _provider = provider;

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetVariable。
    /// 正文中的关键协作调用包括 UpdateVID；具体条件和先后次序见方法体。
    /// </summary>
    internal void SetVariable(string name, object value) => _manager.UpdateVID(name, value);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 AppendVariable。
    /// 正文中的关键协作调用包括 AppendVID；具体条件和先后次序见方法体。
    /// </summary>
    internal void AppendVariable(string name, object value) => _manager.AppendVID(name, value);

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetVariable。
    /// 正文中的关键协作调用包括 GetVIDByName；具体条件和先后次序见方法体。
    /// </summary>
    internal VIDInfo GetVariable(string name) => _manager.GetVIDByName(name);

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetVariableValue。
    /// 正文中的关键协作调用包括 GetValueByName；具体条件和先后次序见方法体。
    /// </summary>
    internal string? GetVariableValue(string name) => _manager.GetValueByName(name);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Refresh。
    /// 正文中的关键协作调用包括 GetVariableValues、TryGetValue；具体条件和先后次序见方法体。
    /// </summary>
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
