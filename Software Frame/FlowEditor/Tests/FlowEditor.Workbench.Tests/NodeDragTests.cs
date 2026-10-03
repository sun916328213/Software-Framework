// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：NodeDragTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、FlowEditor.Controls.ViewModels。导入不代表所有类型都会在运行时加载。
using System.Windows;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.ViewModels;
using Xunit;
using static FlowEditor.Workbench.Tests.TestGraph;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// 节点拖拽功能流畅性与位置精确性测试。
/// 直接测试 NodeViewModel 的 BeginDrag/UpdateDrag/EndDrag 逻辑，
/// 验证位置计算精确、状态切换正确、禁用节点不可拖拽。
/// </summary>
public class NodeDragTests
{
    // ── 基本拖拽流程 ──

    [Fact]
    public void BeginDrag_SetsIsDraggingTrue()
    {
        var (vm, _) = New<CountingNode>("N");
        vm.BeginDrag(new Point(100, 200));
        Assert.True(vm.IsDragging);
    }

    /// <summary>
    /// 验证 EndDrag_SetsIsDraggingFalse 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、BeginDrag、EndDrag、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void EndDrag_SetsIsDraggingFalse()
    {
        var (vm, _) = New<CountingNode>("N");
        vm.BeginDrag(new Point(0, 0));
        vm.EndDrag();
        Assert.False(vm.IsDragging);
    }

    // ── 位置精确性 ──

    [Fact]
    public void UpdateDrag_ComputesCorrectOffset()
    {
        var (vm, model) = New<CountingNode>("N");
        model.X = 50;
        model.Y = 60;

        vm.BeginDrag(new Point(100, 200));
        vm.UpdateDrag(new Point(150, 230));  // dx=50, dy=30

        Assert.Equal(100, model.X);  // 50 + 50
        Assert.Equal(90, model.Y);   // 60 + 30
    }

    /// <summary>
    /// 验证 UpdateDrag_MultipleUpdates_CumulativeFromStart 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、BeginDrag、UpdateDrag、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void UpdateDrag_MultipleUpdates_CumulativeFromStart()
    {
        var (vm, model) = New<CountingNode>("N");
        model.X = 0;
        model.Y = 0;

        vm.BeginDrag(new Point(100, 100));
        vm.UpdateDrag(new Point(120, 110));  // dx=20, dy=10 → (20, 10)
        vm.UpdateDrag(new Point(180, 140));  // dx=80, dy=40 → (80, 40)
        vm.UpdateDrag(new Point(100, 100));  // back to start → (0, 0)

        Assert.Equal(0, model.X);
        Assert.Equal(0, model.Y);
    }

    /// <summary>
    /// 验证 UpdateDrag_NegativeOffset_DecreasesPosition 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、BeginDrag、UpdateDrag、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void UpdateDrag_NegativeOffset_DecreasesPosition()
    {
        var (vm, model) = New<CountingNode>("N");
        model.X = 200;
        model.Y = 200;

        vm.BeginDrag(new Point(500, 500));
        vm.UpdateDrag(new Point(450, 480));  // dx=-50, dy=-20

        Assert.Equal(150, model.X);  // 200 - 50
        Assert.Equal(180, model.Y);  // 200 - 20
    }

    // ── 边界情况 ──

    [Fact]
    public void UpdateDrag_BeforeBeginDrag_NoEffect()
    {
        var (vm, model) = New<CountingNode>("N");
        model.X = 100;
        model.Y = 100;

        vm.UpdateDrag(new Point(500, 500));  // IsDragging=false → no-op

        Assert.Equal(100, model.X);
        Assert.Equal(100, model.Y);
    }

    /// <summary>
    /// 验证 UpdateDrag_AfterEndDrag_NoEffect 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、BeginDrag、EndDrag、UpdateDrag、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void UpdateDrag_AfterEndDrag_NoEffect()
    {
        var (vm, model) = New<CountingNode>("N");
        model.X = 100;
        model.Y = 100;

        vm.BeginDrag(new Point(0, 0));
        vm.EndDrag();
        vm.UpdateDrag(new Point(500, 500));

        Assert.Equal(100, model.X);
        Assert.Equal(100, model.Y);
    }

    // ── 禁用节点 ──

    [Fact]
    public void BeginDrag_DisabledNode_DoesNotStartDrag()
    {
        var (vm, model) = New<CountingNode>("N");
        model.Permission = NodePermission.Disabled;
        Assert.False(vm.IsDraggable);

        vm.BeginDrag(new Point(0, 0));
        Assert.False(vm.IsDragging);
    }

    /// <summary>
    /// 验证 UpdateDrag_DisabledNode_NoPositionChange 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、BeginDrag、UpdateDrag、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void UpdateDrag_DisabledNode_NoPositionChange()
    {
        var (vm, model) = New<CountingNode>("N");
        model.Permission = NodePermission.Disabled;
        model.X = 42;
        model.Y = 42;

        // Disabled 节点 IsDraggable=false，BeginDrag 直接返回
        vm.BeginDrag(new Point(0, 0));
        // 即使强行调用 UpdateDrag 也不应生效（IsDragging 仍为 false）
        vm.UpdateDrag(new Point(999, 999));

        Assert.Equal(42, model.X);
        Assert.Equal(42, model.Y);
    }

    // ── Position 属性联动 ──

    [Fact]
    public void Position_ReflectsModelXY()
    {
        var (vm, model) = New<CountingNode>("N");
        model.X = 123;
        model.Y = 456;

        Assert.Equal(123, vm.Position.X);
        Assert.Equal(456, vm.Position.Y);
    }

    /// <summary>
    /// 验证 Drag_UpdatesPositionProperty 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、BeginDrag、UpdateDrag、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Drag_UpdatesPositionProperty()
    {
        var (vm, model) = New<CountingNode>("N");
        model.X = 0;
        model.Y = 0;

        vm.BeginDrag(new Point(0, 0));
        vm.UpdateDrag(new Point(77, 88));

        Assert.Equal(77, vm.Position.X);
        Assert.Equal(88, vm.Position.Y);
    }

    // ── 零位移 ──

    [Fact]
    public void UpdateDrag_ZeroMovement_StaysAtOriginalPosition()
    {
        var (vm, model) = New<CountingNode>("N");
        model.X = 300;
        model.Y = 400;

        vm.BeginDrag(new Point(250, 250));
        vm.UpdateDrag(new Point(250, 250));  // 零位移

        Assert.Equal(300, model.X);
        Assert.Equal(400, model.Y);
    }

    // ── 多节点独立拖拽 ──

    [Fact]
    public void MultipleNodes_DragIndependently()
    {
        var (vmA, modelA) = New<CountingNode>("A");
        var (vmB, modelB) = New<CountingNode>("B");
        modelA.X = 0; modelA.Y = 0;
        modelB.X = 0; modelB.Y = 0;

        vmA.BeginDrag(new Point(100, 100));
        vmB.BeginDrag(new Point(200, 200));

        vmA.UpdateDrag(new Point(150, 110));  // dx=50, dy=10
        vmB.UpdateDrag(new Point(230, 250));  // dx=30, dy=50

        Assert.Equal(50, modelA.X);
        Assert.Equal(10, modelA.Y);
        Assert.Equal(30, modelB.X);
        Assert.Equal(50, modelB.Y);

        vmA.EndDrag();
        vmB.EndDrag();

        Assert.False(vmA.IsDragging);
        Assert.False(vmB.IsDragging);
    }
}
