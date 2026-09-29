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

    [Fact]
    public void ResizeSelected_UpdatesSize_AndRejectsBadInput()
    {
        var editor = new RoiEditorViewModel();
        editor.AddCircle(0, 0, 10);
        Assert.True(editor.ResizeSelected(30, 30));
        Assert.Equal(new Size2D(30, 30), editor.Selected!.Size);
        Assert.False(editor.ResizeSelected(0, 10));
        Assert.False(editor.ResizeSelected(-1, 10));

        editor.Selected = null;
        Assert.False(editor.ResizeSelected(10, 10));
    }

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