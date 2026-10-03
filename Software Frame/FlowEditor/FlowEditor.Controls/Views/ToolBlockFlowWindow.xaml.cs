// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：ToolBlockFlowWindow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Collections.Generic、System.ComponentModel、System.Windows、FlowEditor.Controls.Interfaces、FlowEditor.Controls.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.ViewModels;

namespace FlowEditor.Controls.Views;

/// <summary>
/// ToolBlock 内部流程可视化编辑器窗口。
/// 包含完整的 FlowChartContainerView，支持多标签页管理多个内部流程。
/// 关闭窗口时自动保存所有标签页数据到 ToolBlockNode。
/// </summary>
public partial class ToolBlockFlowWindow : Window
{
    private readonly IInternalFlowContainerNode _containerNode;
    private readonly IFlowChartService _parentService;
    private readonly FlowChartContainerDocumentService _documentService;
    private readonly FlowChartContainerViewModel _containerVm;

    /// <summary>创建已完成依赖组装的内部流程编辑窗口。</summary>
    /// <param name="node">内部流程容器节点。</param>
    /// <param name="parentService">父流程会话。</param>
    /// <param name="containerViewModel">窗口独占的多流程容器 ViewModel。</param>
    /// <param name="documentService">内部流程文档协调模块。</param>
    public ToolBlockFlowWindow(
        IInternalFlowContainerNode node,
        IFlowChartService parentService,
        FlowChartContainerViewModel containerViewModel,
        FlowChartContainerDocumentService documentService)
    {
        InitializeComponent();
        _containerNode = node ?? throw new ArgumentNullException(nameof(node));
        _parentService = parentService ?? throw new ArgumentNullException(nameof(parentService));
        _containerVm = containerViewModel ?? throw new ArgumentNullException(nameof(containerViewModel));
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        Title = node.InternalEditorTitle;

        _containerVm.ConfigureTabLimits(
            node.MinimumInternalFlows,
            node.MaximumInternalFlows,
            node.InternalFlowNamePrefix);
        FlowContainer.DataContext = _containerVm;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    /// <summary>窗口首次加载时应用工具排除规则并还原内部流程。</summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (_containerVm.ActiveTab != null)
        {
            foreach (var toolName in _containerNode.ExcludedInternalTools)
                _containerVm.ExcludeToolByName(toolName);

            LoadInternalData(_containerVm);
        }
    }

    /// <summary>窗口关闭时自动保存到 ToolBlock。</summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        SaveInternalData(_containerVm);
    }

    /// <summary>将 ToolBlockNode 内部所有流程数据加载到多标签页。</summary>
    private void LoadInternalData(FlowChartContainerViewModel containerVm)
    {
        var data = _containerNode.InternalData;
        if (data == null || data.Count == 0) return;

        // 使用第一个标签页加载第一个流程
        var firstTab = containerVm.ActiveTab!;
        _documentService.LoadFlowToTab(firstTab, data[0]);

        // 后续流程创建新标签页
        for (int i = 1; i < data.Count; i++)
        {
            containerVm.AddNewTab();
            var tab = containerVm.ActiveTab!;
            _documentService.LoadFlowToTab(tab, data[i]);
        }
    }

    /// <summary>保存所有标签页数据到 ToolBlockNode。</summary>
    private void SaveInternalData(FlowChartContainerViewModel containerVm)
    {
        var data = new List<FlowChartDocumentModel>();
        foreach (var tab in containerVm.Tabs)
            data.Add(_documentService.BuildFlowDocument(tab));

        _containerNode.InternalData = data;
        _parentService.Model.IsModified = true;
    }
}
