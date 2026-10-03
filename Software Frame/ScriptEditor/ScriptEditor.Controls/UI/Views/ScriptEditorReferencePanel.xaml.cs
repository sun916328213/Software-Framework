// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptEditorReferencePanel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using System.Windows.Controls;

namespace ScriptEditor.Controls.Views;

/// <summary>
/// 引用管理侧边栏（前后端分离：从 ScriptEditorView 独立出来，延迟创建）。
/// </summary>
public partial class ScriptEditorReferencePanel : UserControl
{
    /// <summary>
    /// 初始化 ScriptEditorReferencePanel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
    /// </summary>
    public ScriptEditorReferencePanel()
    {
        InitializeComponent();
    }
}