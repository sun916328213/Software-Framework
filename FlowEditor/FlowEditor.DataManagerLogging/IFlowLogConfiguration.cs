using DataManager.Log.Definition;

namespace FlowEditor.DataManagerLogging;

/// <summary>向 DataManager 日志 adapter 提供当前流程日志模板及其变更通知。</summary>
public interface IFlowLogConfiguration
{
    /// <summary>当前名为 FlowLog 的基础日志模板；未配置时为空。</summary>
    LogParam? FlowLogTemplate { get; }

    /// <summary>基础日志配置成功替换后触发。</summary>
    event EventHandler? ConfigurationChanged;
}
