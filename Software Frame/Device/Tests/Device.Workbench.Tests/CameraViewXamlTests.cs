// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CameraViewXamlTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench、System.IO、System.Xml.Linq、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace Device.Workbench.Tests;

/// <summary>
/// CameraView.xaml 结构回归：图像框尺寸由 ImageFrame 槽位约束；横向滚动策略与表格自滚动一致。
/// </summary>
public sealed class CameraViewXamlTests
{
    private static readonly XNamespace W =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    /// 测试相关入口 XamlFile_ExistsAtKnownPath；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 True、Exists；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void XamlFile_ExistsAtKnownPath()
    {
        Assert.True(File.Exists(CameraViewPath), $"未找到 CameraView.xaml：{CameraViewPath}");
    }

    /// <summary>
    /// 测试相关入口 ScrollViewer_DisablesPageHorizontalScroll_KeepsVerticalAuto；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Load、First、Descendants、Equal、Attribute；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ScrollViewer_DisablesPageHorizontalScroll_KeepsVerticalAuto()
    {
        var root = Load();
        var scroll = root.Descendants(W + "ScrollViewer").First();

        Assert.Equal("Disabled", scroll.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Equal("Auto", scroll.Attribute("VerticalScrollBarVisibility")?.Value);
    }

    /// <summary>
    /// 测试相关入口 ImageFrame_IsNamedAndClipsToBounds；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Load、First、Descendants、Attribute、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ImageFrame_IsNamedAndClipsToBounds()
    {
        var root = Load();
        var frame = root.Descendants(W + "Border")
            .First(b => (string?)b.Attribute(X + "Name") == "ImageFrame");

        Assert.Equal("True", frame.Attribute("ClipToBounds")?.Value);
        Assert.Equal("#101418", frame.Attribute("Background")?.Value);
    }

    /// <summary>
    /// 测试相关入口 PreviewImage_BoundsToImageFrameActualSize；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Load、First、Descendants、Contains、Attribute；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PreviewImage_BoundsToImageFrameActualSize()
    {
        var root = Load();
        var image = root.Descendants(W + "Image")
            .First(i => ((string?)i.Attribute("Source") ?? string.Empty).Contains("Model.Preview"));

        Assert.Equal(
            "{Binding ActualWidth, ElementName=ImageFrame}",
            image.Attribute("MaxWidth")?.Value);
        Assert.Equal(
            "{Binding ActualHeight, ElementName=ImageFrame}",
            image.Attribute("MaxHeight")?.Value);
        Assert.Equal("Uniform", image.Attribute("Stretch")?.Value);
        Assert.Equal("DownOnly", image.Attribute("StretchDirection")?.Value);
        Assert.Equal("Center", image.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Center", image.Attribute("VerticalAlignment")?.Value);

        // Image 必须是 ImageFrame 的后代，ElementName 绑定才有目标。
        Assert.Contains(
            image.Ancestors(W + "Border"),
            b => (string?)b.Attribute(X + "Name") == "ImageFrame");
    }

    /// <summary>
    /// 测试相关入口 Image_IsNotGivenFixedWidthOrHeight；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Load、First、Descendants、Contains、Attribute；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Image_IsNotGivenFixedWidthOrHeight()
    {
        var root = Load();
        var image = root.Descendants(W + "Image")
            .First(i => ((string?)i.Attribute("Source") ?? string.Empty).Contains("Model.Preview"));

        Assert.Null(image.Attribute("Width"));
        Assert.Null(image.Attribute("Height"));
    }

    /// <summary>
    /// 测试相关入口 TestCard_BindsApplyTriggerAndRuntimeTriggerControls；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Load、Contains、Descendants、Attribute；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void TestCard_BindsApplyTriggerAndRuntimeTriggerControls()
    {
        var root = Load();

        Assert.Contains(
            root.Descendants(W + "Button"),
            b => ((string?)b.Attribute("Command") ?? string.Empty).Contains("ApplyTriggerCommand"));
        Assert.Contains(
            root.Descendants(W + "ComboBox"),
            c => ((string?)c.Attribute("SelectedItem") ?? string.Empty)
                .Contains("Model.RuntimeFrameTriggerMode"));
        Assert.Contains(
            root.Descendants(W + "ComboBox"),
            c => ((string?)c.Attribute("SelectedItem") ?? string.Empty)
                .Contains("Model.RuntimeLineTriggerMode"));
    }

    /// <summary>
    /// 测试相关入口 Load；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Parse、ReadAllText；具体条件和先后次序见方法体。
    /// </summary>
    private static XDocument Load() => XDocument.Parse(File.ReadAllText(CameraViewPath));

    private static string CameraViewPath
    {
        get
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir is not null)
            {
                var direct = Path.Combine(dir.FullName, "Views", "CameraView.xaml");
                if (File.Exists(direct))
                    return direct;

                var inRepo = Path.Combine(dir.FullName, "Device", "Device.Controls", "Views", "CameraView.xaml");
                if (File.Exists(inRepo))
                    return inRepo;

                dir = dir.Parent;
            }

            throw new FileNotFoundException("CameraView.xaml");
        }
    }
}
