using System.Windows.Controls;

namespace ScriptEditor.Views;

/// <summary>
/// 引用管理侧边栏（前后端分离：从 ScriptEditorView 独立出来，延迟创建）。
/// </summary>
public partial class ScriptEditorReferencePanel : UserControl
{
    public ScriptEditorReferencePanel()
    {
        InitializeComponent();
    }
}