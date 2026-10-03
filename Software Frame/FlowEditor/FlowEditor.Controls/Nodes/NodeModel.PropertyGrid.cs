// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core.Nodes、System.Windows.Input、WPFCustomControls.PropertyGrid.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core.Nodes;
using System.Windows.Input;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowEditor.Controls.Nodes;

/// <summary>
/// 将节点模型适配到属性面板扩展契约，并集中保留表现层命令入口。
/// 该兼容桥保持既有第三方节点的接口识别方式不变。
/// </summary>
public abstract partial class NodeModel : IPortConfigurable, IPortManageable, IDataBindingProvider
{
    /// <summary>打开脚本编辑器命令。子类重写后可被 PropertyGridHost 自动检测为扩展按钮。</summary>
    [PropertyGridAction("打开脚本编辑器")]
    public virtual ICommand? OpenScriptEditorCommand => null;
}
