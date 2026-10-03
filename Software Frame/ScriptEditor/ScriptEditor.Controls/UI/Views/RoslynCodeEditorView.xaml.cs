// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：RoslynCodeEditorView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、RoslynPad.Editor、System.Windows、System.Windows.Controls、System.Windows.Media。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using RoslynPad.Editor;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScriptEditor.Controls.Views;

/// <summary>
/// RoslynPad 代码编辑器包装控件。
/// </summary>
/// <remarks>
/// 继承 RoslynCodeEditor，并在主题切换时刷新语法高亮。
/// XAML 配置包含智能提示 ToolTip 样式和默认字体设置。
/// </remarks>
/// <author>ScriptEditor Team</author>
/// <version>1.0</version>
public partial class RoslynCodeEditorView : RoslynCodeEditor
{
    private volatile bool _isDarkTheme;
    internal bool IsDarkTheme => _isDarkTheme;

    /// <summary>
    /// 初始化 RoslynCodeEditorView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
    /// </summary>
    public RoslynCodeEditorView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnPropertyChanged。
    /// 正文中的关键协作调用包括 OnPropertyChanged、RefreshHighlighting；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == BackgroundProperty && e.NewValue is SolidColorBrush brush)
            _isDarkTheme = brush.Color.R + brush.Color.G + brush.Color.B < 384;
        if (e.Property == BackgroundProperty || e.Property == ForegroundProperty)
        {
            if (ClassificationHighlightColors is not null) RefreshHighlighting();
            TextArea?.TextView?.Redraw();
        }
    }
}
