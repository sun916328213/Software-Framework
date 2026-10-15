// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsDataBinding。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Runtime、SecsGem.Runtime.Manager、SecsGem.Runtime.Variable、SecsGem.Core。导入不代表所有类型都会在运行时加载。
using SecsGem.Runtime;
using SecsGem.Runtime.Manager;
using SecsGem.Runtime.Variable;
using SecsGem.Core;
using Extension.Core.Conversion;

namespace SecsGem.Runtime.DataBinding;

/// <summary>VID 存储与设备数据快照的唯一协调入口。</summary>
internal sealed class SecsDataBinding
{
    private readonly VIDManager _manager;
    private readonly StatusManager _status;
    private IEquipmentDataProvider? _provider;

    /// <summary>
    /// 初始化 SecsDataBinding 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    internal SecsDataBinding(VIDManager manager, StatusManager status)
    {
        _manager = manager;
        _status = status;
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
    /// <para>
    /// 修复：原来没配 provider 就直接 return，Clock/DeviceMode/DeviceStatus 永远是
    /// CSV 里的陈旧值（甚至 null），S1F3/S1F11 回出去的全是死的。
    /// 现在系统变量每次都刷（引擎在 S1F3/S1F11 回包前调这里），外部快照有才叠加。
    /// </para>
    /// </summary>
    internal void Refresh()
    {
        RefreshSystemVids();

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

    /// <summary>
    /// 刷新系统变量：Clock=当前时间，模式/状态=状态机当前值。
    /// <para>
    /// 只写存在的变量（缺名或缺变量就跳过，不抛——回包不能因为少个变量就整体失败，
    /// 缺的那个 S1F3 组包时按原有逻辑处理）。名字走 <c>StatusManager</c> 的配置，
    /// 不在这里硬编；Clock 沿用 S6F11 的既有写法。
    /// </para>
    /// </summary>
    private void RefreshSystemVids()
    {
        if (_manager.GetVIDByName("Clock") != null)
            _manager.UpdateVID("Clock", System.DateTime.Now.ToYYYYMMDDHHMMSSFFF());

        var modeName = _status.Mode_VIDName;
        if (!string.IsNullOrEmpty(modeName) && _manager.GetVIDByName(modeName) != null)
            _manager.UpdateVID(modeName, _status.CurrentMode);

        var statusName = _status.Status_VIDName;
        if (!string.IsNullOrEmpty(statusName) && _manager.GetVIDByName(statusName) != null)
            _manager.UpdateVID(statusName, _status.CurrentStatus);
    }
}
