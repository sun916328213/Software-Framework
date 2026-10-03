// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeActionViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows.Input、CommunityToolkit.Mvvm.Input、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;

namespace FlowEditor.Controls.ViewModels;

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
