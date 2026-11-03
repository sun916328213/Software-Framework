using Vision.Controls;
using Vision.Controls.ViewModels;

namespace Vision.Workbench.Tests;

/// <summary>组件测试夹具，不是应用页面/导航/生产装配入口。</summary>
public sealed class TestBrowserViewModel : VisionBrowserViewModel
{
    public TestBrowserViewModel() : base(new Vision.Controls.Services.VisionBrowserWorkspace(
        VisionCatalogFactory.CreateCatalog() with { LoadImage = CreateInput }), ownsWorkspace: true) { }

    // 只提供测试像素；不恢复生产合成图命令或打开桌面选图对话框。
    public static Vision.Core.Imaging.VisionImage CreateInput() =>
        Vision.Tests.Tools.SyntheticImages.WhiteRectangle(64, 64, 10, 10, 30, 30);
}
