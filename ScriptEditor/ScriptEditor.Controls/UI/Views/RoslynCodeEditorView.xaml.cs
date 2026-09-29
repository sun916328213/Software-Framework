using RoslynPad.Editor;

namespace ScriptEditor.Views;

/// <summary>
/// RoslynPad 代码编辑器包装控件。
/// </summary>
/// <remarks>
/// 继承 RoslynCodeEditor 以支持 XAML 中使用，无额外逻辑。
/// XAML 配置包含智能提示 ToolTip 样式和默认字体设置。
/// </remarks>
/// <author>ScriptEditor Team</author>
/// <version>1.0</version>
public partial class RoslynCodeEditorView : RoslynCodeEditor
{
    public RoslynCodeEditorView()
    {
        InitializeComponent();
    }
}