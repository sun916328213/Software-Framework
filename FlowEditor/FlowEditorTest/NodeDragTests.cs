using System.Windows;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

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
