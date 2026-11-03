// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：RoiEditorViewModelTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Workbench、Vision.Workbench.Tests、Vision.Controls.ViewModels、Vision.Core.Geometry、Vision.Core.Roi。导入不代表所有类型都会在运行时加载。

using Vision.Workbench.Tests;
// ─────────────────────────────────────────────────────────────────────────────
// P8 测试 —— RoiEditorViewModel（增删改 + 跟随 Fixture，纯逻辑）.
// 原先住在 Vision.Tests；按分层规则「UI 层测试独占 WPF」迁到本工程。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Roi;

namespace Vision.Workbench.Tests.Controls;

/// <summary>ROI 编辑器 VM 测试。</summary>
[Trait("Category", "Unit")]
public sealed class RoiEditorViewModelTests
{
    /// <summary>
    /// 测试相关入口 Add_SelectsNewRoi_AndTracksCount；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 AddRectangle、Equal、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Add_SelectsNewRoi_AndTracksCount()
    {
        var editor = new RoiEditorViewModel();
        var roi = editor.AddRectangle(10, 10, 20, 10);
        Assert.Equal(1, editor.Count);
        Assert.Same(roi, editor.Selected);
        Assert.Equal(RoiKind.Rectangle, roi.Kind);
    }

    [Fact]
    public void DefaultRois_UseTwoHundredByOneHundredSize_AndRotatedRectangleStartsAtZeroDegrees()
    {
        var editor = new RoiEditorViewModel();

        editor.AddDefaultRectangle();
        Assert.Equal(new Size2D(200, 100), editor.Selected!.Size);

        editor.AddDefaultRotatedRectangle();
        Assert.Equal(new Size2D(200, 100), editor.Selected!.Size);
        Assert.Equal(0, editor.Selected.Pose.AngleDeg);

        editor.AddDefaultEllipse();
        Assert.Equal(new Size2D(200, 100), editor.Selected!.Size);

        editor.AddDefaultCircle();
        Assert.Equal(new Size2D(100, 100), editor.Selected!.Size);
    }

    /// <summary>
    /// 测试相关入口 RemoveSelected_ClearsSelection；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 AddRectangle、AddCircle、Equal、Execute、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RemoveSelected_ClearsSelection()
    {
        var editor = new RoiEditorViewModel();
        editor.AddRectangle(10, 10, 20, 10);
        editor.AddCircle(0, 0, 10);
        Assert.Equal(2, editor.Count);

        editor.RemoveSelectedCommand.Execute(null);
        Assert.Equal(1, editor.Count);
        Assert.Null(editor.Selected);

        editor.ClearCommand.Execute(null);
        Assert.Equal(0, editor.Count);
    }

    /// <summary>
    /// 测试相关入口 MoveSelected_ShiftsPose_AndRejectsWithoutSelection；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 False、MoveSelected、AddRectangle、True、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MoveSelected_ShiftsPose_AndRejectsWithoutSelection()
    {
        var editor = new RoiEditorViewModel();
        Assert.False(editor.MoveSelected(5, 5));

        editor.AddRectangle(10, 10, 20, 10);
        Assert.True(editor.MoveSelected(5, -3));
        Assert.Equal(15, editor.Selected!.Pose.X);
        Assert.Equal(7, editor.Selected.Pose.Y);
        Assert.Equal(20, editor.Selected.Size.Width);
    }

    /// <summary>
    /// 测试相关入口 ResizeSelected_UpdatesSize_AndRejectsBadInput；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 AddCircle、True、ResizeSelected、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ResizeSelected_UpdatesSize_AndRejectsBadInput()
    {
        var editor = new RoiEditorViewModel();
        editor.AddCircle(0, 0, 10);
        Assert.True(editor.ResizeSelected(30, 30));
        Assert.Equal(new Size2D(30, 30), editor.Selected!.Size);
        Assert.False(editor.ResizeSelected(0, 10));
        Assert.False(editor.ResizeSelected(-1, 10));
        Assert.False(editor.ResizeSelected(20, 30));
        Assert.Equal(new Size2D(30, 30), editor.Selected.Size);

        editor.Selected = null;
        Assert.False(editor.ResizeSelected(10, 10));
    }

    [Fact]
    public void PointerEditing_SelectsTopmost_MovesInImagePixels_AndCanCancel()
    {
        var editor = new RoiEditorViewModel();
        var first = editor.AddRectangle(20, 20, 30, 30);
        var top = editor.AddRectangle(20, 20, 10, 10);
        Assert.True(editor.TryBeginManipulation(new(20, 20), 1));
        Assert.Same(top, editor.Selected);
        editor.UpdateManipulation(new(27, 16));
        Assert.Equal(27, editor.SelectedX);
        Assert.Equal(16, editor.SelectedY);
        editor.EndManipulation(cancel: true);
        Assert.Equal(new Pose2D(20, 20, 0), top.Pose);
        Assert.Equal(new Pose2D(20, 20, 0), first.Pose);
        Assert.True(editor.TryBeginManipulation(new(20, 20), 1));
        editor.UpdateManipulation(new(22, 23));
        editor.EndManipulation();
        Assert.Equal(new Pose2D(22, 23, 0), top.Pose);
        Assert.False(editor.TryBeginManipulation(new(100, 100), 1));
        Assert.Null(editor.Selected);
    }

    [Fact]
    public void RectangleCornerResize_KeepsOppositeCornerFixed_AndCircleStaysCircular()
    {
        var editor = new RoiEditorViewModel();
        var rectangle = editor.AddRectangle(20, 20, 20, 10);
        Assert.True(editor.TryBeginManipulation(new(30, 25), 0.5));
        editor.UpdateManipulation(new(40, 35));
        editor.EndManipulation();
        Assert.Equal(new Size2D(30, 20), rectangle.Size);
        Assert.Equal(new Pose2D(25, 25, 0), rectangle.Pose);
        Assert.Equal(10, rectangle.Bounds.X);
        Assert.Equal(15, rectangle.Bounds.Y);
        var circle = editor.AddCircle(60, 60, 10);
        Assert.True(editor.TryBeginManipulation(new(65, 60), 0.5));
        editor.UpdateManipulation(new(70, 60));
        editor.EndManipulation();
        Assert.Equal(new Pose2D(60, 60, 0), circle.Pose);
        Assert.Equal(new Size2D(20, 20), circle.Size);
        editor.SelectedHeight = 30;
        Assert.Equal(new Size2D(30, 30), circle.Size);
    }

    [Fact]
    public void RotatedRectangleResize_UsesLocalAxes_NotImageBoundingBox()
    {
        var editor = new RoiEditorViewModel();
        var roi = Roi.RotatedRectangle(50, 50, 20, 10, 90);
        editor.Rois.Add(roi);
        editor.Selected = roi;
        var original = roi.Pose;
        Assert.True(editor.TryBeginManipulation(original.Transform(new(10, 5)), 0.1));
        editor.UpdateManipulation(original.Transform(new(20, 10)));
        editor.EndManipulation();
        Assert.Equal(30, roi.Size.Width, 8);
        Assert.Equal(15, roi.Size.Height, 8);
        var anchor = roi.Pose.Transform(new(-roi.Size.Width / 2, -roi.Size.Height / 2));
        var expected = original.Transform(new(-10, -5));
        Assert.Equal(expected.X, anchor.X, 8);
        Assert.Equal(expected.Y, anchor.Y, 8);
    }

    [Fact]
    public void SelectionChangeAndRemoval_CancelEditing_AndNumericEditsNotifyBindings()
    {
        var editor = new RoiEditorViewModel();
        var first = editor.AddRectangle(20, 20, 10, 10);
        var second = editor.AddCircle(60, 60, 10);
        Assert.True(editor.TryBeginManipulation(new(20, 20), 0.1));
        editor.UpdateManipulation(new(30, 30));
        editor.Selected = second;
        Assert.False(editor.IsManipulating);
        Assert.Equal(new Pose2D(20, 20, 0), first.Pose);
        var changed = new List<string?>();
        editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        editor.SelectedX = 70;
        Assert.Equal(70, second.Pose.X);
        Assert.Contains(nameof(editor.SelectedX), changed);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SelectedWidth = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SelectedHeight = -1);
        Assert.Equal(new Size2D(10, 10), second.Size);
        Assert.True(editor.TryBeginManipulation(new(70, 60), 0.1));
        editor.UpdateManipulation(new(80, 80));
        editor.Rois.Remove(second);
        Assert.Null(editor.Selected);
        Assert.False(editor.IsManipulating);
        Assert.False(editor.UpdateManipulation(new(90, 90)));
    }

    [Fact]
    public void PolygonMove_UpdatesVertices_NotOnlyItsPose()
    {
        var editor = new RoiEditorViewModel();
        var polygon = Roi.Polygon([new(10, 10), new(20, 10), new(20, 20), new(10, 20)]);
        editor.Rois.Add(polygon);
        Assert.True(editor.TryBeginManipulation(new(15, 15), 0));
        editor.UpdateManipulation(new(18, 19));
        editor.EndManipulation();
        Assert.Equal(new Point2D(13, 14), polygon.OutlinePoints[0]);
        Assert.Equal(13, polygon.Bounds.X);
        Assert.Equal(14, polygon.Bounds.Y);
        Assert.False(editor.CanResizeSelected);
    }

    /// <summary>
    /// 测试相关入口 FollowFixture_MovesAll_AndKeepsSelection；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 AddRectangle、AddCircle、FollowFixture、Equal、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FollowFixture_MovesAll_AndKeepsSelection()
    {
        var editor = new RoiEditorViewModel();
        editor.AddRectangle(10, 10, 20, 10);
        editor.AddCircle(0, 0, 10);
        var selected = editor.Selected;

        editor.FollowFixture(new Pose2D(0, 0, 0), new Pose2D(100, 0, 0));
        Assert.Equal(2, editor.Count);
        Assert.Equal(110, editor.Rois[0].Pose.X);
        Assert.Equal(100, editor.Rois[1].Pose.X);
        Assert.Same(editor.Rois[1], editor.Selected);
        Assert.NotSame(selected, editor.Selected);
    }
}