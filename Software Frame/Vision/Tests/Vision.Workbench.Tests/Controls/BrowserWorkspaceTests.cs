// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：BrowserWorkspaceTests、CountingTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Controls.Models、Vision.Controls.Services、Vision.Controls.ViewModels、Vision.Core.Imaging、Vision.Core.Results。导入不代表所有类型都会在运行时加载。
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Workbench.Tests.Controls;

/// <summary>
/// BrowserWorkspaceTests 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class BrowserWorkspaceTests
{
    /// <summary>
    /// 测试相关入口 SharedWorkspace_SurvivesOneViewModelDisposal_AndReleasesCardsOnce；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Same、Dispose、Equal、UseDemoImage、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void SharedWorkspace_SurvivesOneViewModelDisposal_AndReleasesCardsOnce()
    {
        var firstTool = new CountingTool();
        var secondTool = new CountingTool();
        var entries = new[]
        {
            new ToolEntry("First", "Test", () => (firstTool, new object(), null)),
            new ToolEntry("Second", "Test", () => (secondTool, new object(), null))
        };
        using var workspace = new VisionBrowserWorkspace(new(entries, [], _ => throw new InvalidOperationException(), () => null, _ => "Compared"));
        using var first = new VisionBrowserViewModel(workspace);
        using var second = new VisionBrowserViewModel(workspace);
        Assert.Same(first.Model, second.Model);
        first.Dispose();
        Assert.Equal(0, firstTool.DisposeCount);
        second.UseDemoImage();
        Assert.Contains("合成图", second.ImageInfoText);
        second.SelectedEntry = entries[1];
        Assert.Equal(1, firstTool.DisposeCount);
        Assert.Same(entries[1], workspace.Model.SelectedEntry);
        second.CurrentCard!.Run();
        Assert.Equal("执行成功", second.CurrentCard.StatusText);
        workspace.Dispose();
        workspace.Dispose();
        Assert.Equal(1, secondTool.DisposeCount);
    }

    /// <summary>
    /// 测试相关入口 Commands_FollowImageToolAndDisposalState_WithoutClosingBorrowedWorkspace；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 False、CanExecute、True、Execute、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Commands_FollowImageToolAndDisposalState_WithoutClosingBorrowedWorkspace()
    {
        var tool = new CountingTool();
        var entries = new[] { new ToolEntry("Test", "Test", () => (tool, new object(), null)) };
        using var workspace = new VisionBrowserWorkspace(new(entries, [], _ => throw new InvalidOperationException(), () => null, _ => "Compared"));
        using var first = new VisionBrowserViewModel(workspace);
        using var second = new VisionBrowserViewModel(workspace);
        Assert.False(first.CompareBlobCommand.CanExecute(null));
        Assert.True(first.RunBenchmarkCommand.CanExecute(null));
        first.UseDemoImageCommand.Execute(null);
        Assert.True(second.CompareBlobCommand.CanExecute(null));
        first.Dispose();
        Assert.False(first.LoadImageCommand.CanExecute(null));
        Assert.False(first.UseDemoImageCommand.CanExecute(null));
        Assert.False(first.CompareBlobCommand.CanExecute(null));
        Assert.False(first.RunBenchmarkCommand.CanExecute(null));
        first.RunBenchmarkCommand.Execute(null); // ICommand.Execute 不自动检查 CanExecute。
        Assert.Equal(0, tool.RunCount);
        Assert.True(second.RunBenchmarkCommand.CanExecute(null));
        var oldCard = second.CurrentCard!;
        second.SelectedEntry = null;
        Assert.False(second.RunBenchmarkCommand.CanExecute(null));
        Assert.False(oldCard.RunCommand.CanExecute(null));
        Assert.False(oldCard.RefreshValidationCommand.CanExecute(null));
        Assert.False(oldCard.EnsureLoadedCommand.CanExecute(null));
        oldCard.RunCommand.Execute(null);
        oldCard.RefreshValidationCommand.Execute(null);
        oldCard.EnsureLoadedCommand.Execute(null);
        Assert.Equal(0, tool.RunCount);
        Assert.Equal(1, tool.ValidationCount);
        Assert.Equal(0, tool.LoadCount);
        Assert.Equal(1, tool.DisposeCount);
        workspace.Dispose();
        Assert.False(second.LoadImageCommand.CanExecute(null));
        Assert.False(second.CompareBlobCommand.CanExecute(null));
        Assert.Null(workspace.Model.CurrentParameters);
        Assert.Throws<ObjectDisposedException>(workspace.UseDemoImage);
    }

    /// <summary>
    /// 测试相关入口 DisposedToolWorkspace_RejectsDirectCallsBeforeTouchingTool；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Dispose、Throws、SetImage、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DisposedToolWorkspace_RejectsDirectCallsBeforeTouchingTool()
    {
        var tool = new CountingTool();
        var workspace = new ToolCardWorkspace(tool);
        workspace.Dispose();
        workspace.Dispose();
        Assert.Throws<ObjectDisposedException>(() => workspace.SetImage(null));
        Assert.Throws<ObjectDisposedException>(workspace.Run);
        Assert.Throws<ObjectDisposedException>(workspace.RefreshValidation);
        Assert.Throws<ObjectDisposedException>(workspace.EnsureLoaded);
        Assert.Equal(0, tool.RunCount);
        Assert.Equal(0, tool.ValidationCount);
        Assert.Equal(0, tool.LoadCount);
        Assert.Equal(1, tool.DisposeCount);
    }

    /// <summary>
    /// CountingTool 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IExecutableTool。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class CountingTool : IExecutableTool
    {
        public string Name => "Test";
        public long LastElapsedMs => 0;
        public int DisposeCount { get; private set; }
        public int RunCount { get; private set; }
        public int ValidationCount { get; private set; }
        public int LoadCount { get; private set; }
        /// <summary>
        /// 测试相关入口 Validate；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Success；具体条件和先后次序见方法体。
        /// </summary>
        public ValidationResult Validate() { ValidationCount++; return ValidationResult.Success(); }
        /// <summary>
        /// 测试相关入口 Run；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public ToolResult Run(VisionImage? image) { RunCount++; return new(); }
        /// <summary>
        /// 测试相关入口 EnsureLoaded；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void EnsureLoaded() { LoadCount++; }
        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Dispose() => DisposeCount++;
    }
}
