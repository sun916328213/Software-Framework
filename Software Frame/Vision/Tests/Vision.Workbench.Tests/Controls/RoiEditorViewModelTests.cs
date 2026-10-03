// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：RoiEditorViewModelTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Workbench、Vision.Workbench.Tests、Vision.Controls.ViewModels、Vision.Core.Geometry、Vision.Core.Roi。导入不代表所有类型都会在运行时加载。
using Vision.Workbench;
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
    /// 验证 Add_SelectsNewRoi_AndTracksCount 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
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

    /// <summary>
    /// 验证 RemoveSelected_ClearsSelection 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
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
    /// 验证 MoveSelected_ShiftsPose_AndRejectsWithoutSelection 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
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
    /// 验证 ResizeSelected_UpdatesSize_AndRejectsBadInput 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
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

        editor.Selected = null;
        Assert.False(editor.ResizeSelected(10, 10));
    }

    /// <summary>
    /// 验证 FollowFixture_MovesAll_AndKeepsSelection 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
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