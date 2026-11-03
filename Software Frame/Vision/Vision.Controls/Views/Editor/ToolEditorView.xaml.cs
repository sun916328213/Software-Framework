// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ToolEditorView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Windows、System.Windows.Controls、Vision.Core.Tools、Vision.Controls.ViewModels、System.ComponentModel。导入不代表所有类型都会在运行时加载。
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Vision.Core.Tools;
using Vision.Controls.ViewModels;

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
    private BlobParametersEditorViewModel _blobEditor;
    private IEditableTool _observedTool;
    private void DetachParameterNotifications()
    {
        if (_observedTool != null) PropertyChangedEventManager.RemoveHandler(_observedTool, OnToolParametersChanged, string.Empty);
        _observedTool = null;
    }
    private void OnToolParametersChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, Tool) && (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(IEditableTool.EditableParameters))) RebuildParameterEditor();
    }
    private static readonly DependencyPropertyKey ParameterEditorPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ParameterEditor), typeof(object), typeof(ToolEditorView), new PropertyMetadata(null));
    public static readonly DependencyProperty ParameterEditorProperty = ParameterEditorPropertyKey.DependencyProperty;
    public object ParameterEditor => GetValue(ParameterEditorProperty);
    public ToolEditorView()
    {
        InitializeComponent();
        Loaded += (_, _) => RebuildParameterEditor();
        Unloaded += (_, _) => { DetachParameterNotifications(); _blobEditor?.Dispose(); _blobEditor = null; SetValue(ParameterEditorPropertyKey, null); };
    }
    private void RebuildParameterEditor()
    {
        _blobEditor?.Dispose(); _blobEditor = null;
        var tool = Tool;
        DetachParameterNotifications();
        if (IsLoaded && tool != null) { _observedTool = tool; PropertyChangedEventManager.AddHandler(tool, OnToolParametersChanged, string.Empty); }
        if (tool != null) _blobEditor = BlobParametersEditorViewModel.TryCreate(tool.EditableParameters,
            () => { if (ReferenceEquals(Tool, tool)) Status.Text = "参数已变化，请重新执行"; });
        SetValue(ParameterEditorPropertyKey, (object)_blobEditor ?? tool?.EditableParameters);
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ToolChanged。
    /// </summary>
    private void MetadataParametersChanged(object sender, EventArgs e)
    {
        if (sender is VisionPropertyPanel panel && ReferenceEquals(panel.TargetObject, ParameterEditor))
            Status.Text = "参数已变化，请重新执行";
    }
    private static void ToolChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (ToolEditorView)sender;
        view.Status.Text = string.Empty;
        view.RebuildParameterEditor();
        var preparation = args.NewValue as IModelPreparationTool;
        view.ModelButton.Visibility = preparation == null ? Visibility.Collapsed : Visibility.Visible;
        view.ModelButton.Content = preparation?.PreparationKind == ModelPreparationKind.Train ? "训练模型" : "加载模型";
    }
    /// <summary>
    /// 执行加载操作；对应入口为 LoadClick。
    /// 正文中的关键协作调用包括 Execute、PrepareModel；具体条件和先后次序见方法体。
    /// </summary>
    private void LoadClick(object sender, RoutedEventArgs e) => Execute(tool =>
    {
        if (tool is not IModelPreparationTool preparation) return;
        preparation.PrepareModel();
        if (ReferenceEquals(Tool, tool)) Status.Text = preparation.PreparationKind == ModelPreparationKind.Train ? "模型已训练" : "模型已加载";
    });
    /// <summary>
    /// 运行当前处理流程；对应入口为 RunClick。
    /// 正文中的关键协作调用包括 Execute、Run；具体条件和先后次序见方法体。
    /// </summary>
    private void RunClick(object sender, RoutedEventArgs e) => Execute(tool =>
    {
        var validation = tool.Validate();
        if (!ReferenceEquals(Tool, tool)) return;
        if (!validation.IsValid) { Status.Text = validation.ToMessage(); return; }
        tool.Run();
        if (ReferenceEquals(Tool, tool)) Status.Text = $"{tool.ToolState} {tool.RunMessage}";
    });
    /// <summary>
    /// 执行保存操作；对应入口为 SaveClick。
    /// 正文中的关键协作调用包括 Execute、Save；具体条件和先后次序见方法体。
    /// </summary>
    private void SaveClick(object sender, RoutedEventArgs e) => Execute(tool =>
    {
        tool.Save(); if (ReferenceEquals(Tool, tool)) Status.Text = "已保存";
    });
    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 Execute。
    /// 正文中的关键协作调用包括 action；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private void Execute(Action<IEditableTool> action)
    {
        var tool = Tool;
        if (tool is null) return;
        try { action(tool); } catch (Exception ex) { if (ReferenceEquals(Tool, tool)) Status.Text = ex.Message; }
    }
}
