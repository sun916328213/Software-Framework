using System.Windows.Input;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes;

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
