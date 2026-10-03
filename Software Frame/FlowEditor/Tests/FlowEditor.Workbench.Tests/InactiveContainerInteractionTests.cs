// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：InactiveContainerInteractionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO、System.Windows、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Nodes.FlowControl。导入不代表所有类型都会在运行时加载。
using System.IO;
using System.Windows;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.ViewModels;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// InactiveContainerInteractionTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class InactiveContainerInteractionTests
{
    /// <summary>
    /// 验证 DragContainer_MovesContainedNodesBySameDelta 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、CreateViewModel、AddNode、SetSelectedNodes、BeginNodeDrag；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DragContainer_MovesContainedNodesBySameDelta()
    {
        var service = TestFlowEditorFactory.CreateService();
        var vm = TestFlowEditorFactory.CreateViewModel(service);
        var container = service.Canvas.AddNode(new InactiveNodeContainerNode { X = 100, Y = 100 });
        var child = service.Canvas.AddNode(new CountingNode
        {
            Name = "Child", X = 150, Y = 200, IsExcludedFromExecution = true
        });

        vm.SetSelectedNodes([container]);
        vm.BeginNodeDrag(container, new Point(100, 100));
        vm.UpdateNodeDrag(container, new Point(130, 140));
        vm.EndNodeDrag(container);

        Assert.Equal(130, container.X);
        Assert.Equal(140, container.Y);
        Assert.Equal(180, child.X);
        Assert.Equal(240, child.Y);
    }

    /// <summary>
    /// 验证 Container_ExposesResizableDimensions 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetProperty、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Container_ExposesResizableDimensions()
    {
        var width = typeof(InactiveNodeContainerNode).GetProperty("ContainerWidth");
        var height = typeof(InactiveNodeContainerNode).GetProperty("ContainerHeight");

        Assert.True(width?.CanWrite == true);
        Assert.True(height?.CanWrite == true);
    }

    /// <summary>
    /// 验证 ContainerView_UsesSameSelectedStateAsNodes_AndCanvasSizeIsBound 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ReadAllText、Combine、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ContainerView_UsesSameSelectedStateAsNodes_AndCanvasSizeIsBound()
    {
        var root = TestModulePaths.ModuleDirectory;
        var containerXaml = File.ReadAllText(Path.Combine(root,
            "FlowEditor.Controls", "Views", "InactiveNodeContainerView.xaml"));
        var chartXaml = File.ReadAllText(Path.Combine(root,
            "FlowEditor.Controls", "Views", "FlowChartView.xaml"));

        Assert.Contains("x:Name=\"SelectionIndicator\"", containerXaml);
        Assert.Contains("Visibility=\"{Binding IsSelected", containerXaml);
        Assert.Contains("BorderBrush=\"{Binding Status", containerXaml);
        Assert.Contains("Width=\"{Binding Model.CanvasWidth}\"", chartXaml);
        Assert.Contains("Height=\"{Binding Model.CanvasHeight}\"", chartXaml);
    }
}
