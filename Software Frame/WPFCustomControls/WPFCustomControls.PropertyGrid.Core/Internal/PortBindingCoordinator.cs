// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：PortBindingCoordinator。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls.PropertyGrid.Core.Models。导入不代表所有类型都会在运行时加载。
using WPFCustomControls.PropertyGrid.Core.Models;

namespace WPFCustomControls.PropertyGrid.Core.Internal;

/// <summary>Centralizes binding lookup and replace/remove invariants.</summary>
/// <remarks>
/// PortBindingCoordinator 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// </remarks>
internal sealed class PortBindingCoordinator
{
    private readonly IDataBindingProvider _provider;

    /// <summary>
    /// 初始化 PortBindingCoordinator 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public PortBindingCoordinator(IDataBindingProvider provider) => _provider = provider;

    public IReadOnlyList<DataBindingInfo> Bindings => _provider.GetDataBindings();
    public IReadOnlyList<UpstreamNodeInfo> UpstreamNodes => _provider.GetUpstreamFlowNodes();
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetUpstreamPorts。
    /// 正文中的关键协作调用包括 GetUpstreamOutputPorts；具体条件和先后次序见方法体。
    /// </summary>
    public IReadOnlyList<string> GetUpstreamPorts(string nodeId) => _provider.GetUpstreamOutputPorts(nodeId);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Find。
    /// 正文中的关键协作调用包括 Find；具体条件和先后次序见方法体。
    /// </summary>
    public DataBindingInfo? Find(string portId, string propertyName) =>
        Find(Bindings, portId, propertyName);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Find。
    /// 正文中的关键协作调用包括 FirstOrDefault；具体条件和先后次序见方法体。
    /// </summary>
    public static DataBindingInfo? Find(
        IEnumerable<DataBindingInfo> bindings,
        string portId,
        string propertyName) =>
        bindings.FirstOrDefault(binding => binding.TargetPortId == portId)
        ?? bindings.FirstOrDefault(binding => binding.PropertyName == propertyName);

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 Create。
    /// 正文中的关键协作调用包括 CreateDataBinding；具体条件和先后次序见方法体。
    /// </summary>
    public void Create(PortDialogResult result, string targetPortName)
    {
        if (result is not
            {
                IsUpstreamData: true,
                UpstreamNodeId: not null,
                UpstreamPortName: not null
            })
            return;
        _provider.CreateDataBinding(result.UpstreamNodeId, result.UpstreamPortName, targetPortName);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Replace。
    /// 正文中的关键协作调用包括 RemoveDataBinding、CreateDataBinding；具体条件和先后次序见方法体。
    /// </summary>
    public void Replace(DataBindingInfo? existing, PortDialogResult result, string targetPortName)
    {
        if (result is not
            {
                IsUpstreamData: true,
                UpstreamNodeId: not null,
                UpstreamPortName: not null
            })
            return;
        if (existing != null)
            _provider.RemoveDataBinding(existing.BindingId);
        _provider.CreateDataBinding(result.UpstreamNodeId, result.UpstreamPortName, targetPortName);
    }

    /// <summary>
    /// 执行移除操作；对应入口为 Remove。
    /// 正文中的关键协作调用包括 Find、RemoveDataBinding；具体条件和先后次序见方法体。
    /// </summary>
    public bool Remove(string portId, string propertyName)
    {
        var binding = Find(portId, propertyName);
        if (binding == null)
            return false;
        _provider.RemoveDataBinding(binding.BindingId);
        return true;
    }
}
