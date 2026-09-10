using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.Services;
using FlowEditor.ViewModels;
using Prism.Ioc;

namespace FlowEditor.Views;

/// <summary>
/// ToolBlock 内部流程可视化编辑器窗口。
/// 包含完整的 FlowChartContainerView，支持多标签页管理多个内部流程。
/// 关闭窗口时自动保存所有标签页数据到 ToolBlockNode。
/// </summary>
public partial class ToolBlockFlowWindow : Window
{
    private readonly IInternalFlowContainerNode _containerNode;
    private readonly IFlowChartService _parentService;
    private readonly IContainerProvider _containerProvider;
    private FlowChartContainerDocumentService? _documentService;
    private FlowChartContainerViewModel? _containerVm;

    public ToolBlockFlowWindow(ToolBlockNode node, IFlowChartService parentService)
        : this((IInternalFlowContainerNode)node, parentService)
    {
    }

    public ToolBlockFlowWindow(IInternalFlowContainerNode node, IFlowChartService parentService)
    {
        InitializeComponent();
        _containerNode = node;
        _parentService = parentService;
        _containerProvider = parentService.ContainerProvider!;
        Title = node.InternalEditorTitle;

        // 通过 IoC 容器创建独立的容器 ViewModel 实例，不共享主窗口的实例
        _containerVm = _containerProvider.Resolve<FlowChartContainerViewModel>();
        _containerVm.ConfigureTabLimits(
            node.MinimumInternalFlows,
            node.MaximumInternalFlows,
            node.InternalFlowNamePrefix);
        _documentService = _containerProvider.Resolve<FlowChartContainerDocumentService>();
        FlowContainer.DataContext = _containerVm;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (_containerVm?.ActiveTab != null)
        {
            foreach (var toolName in _containerNode.ExcludedInternalTools)
                _containerVm.ExcludeToolByName(toolName);

            LoadInternalData(_containerVm);
        }
    }

    /// <summary>窗口关闭时自动保存到 ToolBlock。</summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_containerVm != null)
            SaveInternalData(_containerVm);
    }

    /// <summary>将 ToolBlockNode 内部所有流程数据加载到多标签页。</summary>
    private void LoadInternalData(FlowChartContainerViewModel containerVm)
    {
        if (_documentService == null) return;
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
        if (_documentService == null) return;

        var data = new List<FlowChartDocumentModel>();
        foreach (var tab in containerVm.Tabs)
            data.Add(_documentService.BuildFlowDocument(tab));

        _containerNode.InternalData = data;
        _parentService.Model.IsModified = true;
    }
}
