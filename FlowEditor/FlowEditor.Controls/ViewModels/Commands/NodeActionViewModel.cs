using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using FlowEditor.Core;

namespace FlowEditor.ViewModels;

/// <summary>把纯节点动作描述适配为 WPF 可绑定命令。</summary>
public sealed class NodeActionViewModel
{
    /// <summary>创建节点动作的表现层 adapter。</summary>
    /// <param name="descriptor">节点提供的动作元数据。</param>
    /// <param name="execute">经节点动作入口执行该标识的回调。</param>
    public NodeActionViewModel(NodeActionDescriptor descriptor, Action<string> execute)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(execute);

        Id = descriptor.Id;
        Name = descriptor.Name;
        IconResource = descriptor.IconResource;
        Command = new RelayCommand(() => execute(descriptor.Id));
    }

    /// <summary>动作标识。</summary>
    public string Id { get; }

    /// <summary>用户可见名称。</summary>
    public string Name { get; }

    /// <summary>图标资源键。</summary>
    public string IconResource { get; }

    /// <summary>WPF 按钮绑定的执行命令。</summary>
    public ICommand Command { get; }
}
