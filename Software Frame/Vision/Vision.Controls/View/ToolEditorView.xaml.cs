// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ToolEditorView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Windows、System.Windows.Controls、Vision.Core.Tools、Vision.Controls.ViewModels、Vision.Tools.Halcon.Sessions。导入不代表所有类型都会在运行时加载。
using System;
using System.Windows;
using System.Windows.Controls;
using Vision.Core.Tools;
using Vision.Controls.ViewModels;
using Vision.Tools.Halcon.Sessions;

namespace Vision.Controls.View;

/// <summary>
/// ToolEditorView 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：UserControl。具体约束按这些契约及各成员实现解释。
/// </summary>
public partial class ToolEditorView : UserControl
{
    public static readonly DependencyProperty ToolProperty = DependencyProperty.Register(
        nameof(Tool), typeof(IEditableTool), typeof(ToolEditorView), new PropertyMetadata(null, ToolChanged));
    public IEditableTool Tool { get => (IEditableTool)GetValue(ToolProperty); set => SetValue(ToolProperty, value); }
    /// <summary>
    /// 初始化 ToolEditorView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
    /// </summary>
    public ToolEditorView() => InitializeComponent();
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ToolChanged。
    /// </summary>
    private static void ToolChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (ToolEditorView)sender;
        view.ModelButton.Visibility = args.NewValue is ShapeMatchSession or DetectionToolSession
            ? Visibility.Visible : Visibility.Collapsed;
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ValidateClick。
    /// 正文中的关键协作调用包括 Execute、Validate、ToMessage；具体条件和先后次序见方法体。
    /// </summary>
    private void ValidateClick(object sender, RoutedEventArgs e) => Execute(() =>
    {
        var result = Tool.Validate();
        Status.Text = result.IsValid ? "校验通过" : result.ToMessage();
    });
    /// <summary>
    /// 执行加载操作；对应入口为 LoadClick。
    /// 正文中的关键协作调用包括 Execute、Config；具体条件和先后次序见方法体。
    /// </summary>
    private void LoadClick(object sender, RoutedEventArgs e) => Execute(() => { Tool.Config(); Status.Text = "已加载 / 训练"; });
    /// <summary>
    /// 运行当前处理流程；对应入口为 RunClick。
    /// 正文中的关键协作调用包括 Execute、Run；具体条件和先后次序见方法体。
    /// </summary>
    private void RunClick(object sender, RoutedEventArgs e) => Execute(() => { Tool.Run(); Status.Text = $"{Tool.ToolState} {Tool.RunMessage}"; });
    /// <summary>
    /// 执行保存操作；对应入口为 SaveClick。
    /// 正文中的关键协作调用包括 Execute、Save；具体条件和先后次序见方法体。
    /// </summary>
    private void SaveClick(object sender, RoutedEventArgs e) => Execute(() => { Tool.Save(); Status.Text = "已保存"; });
    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 Execute。
    /// 正文中的关键协作调用包括 action；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private void Execute(Action action)
    {
        if (Tool is null) return;
        try { action(); } catch (Exception ex) { Status.Text = ex.Message; }
    }
}
