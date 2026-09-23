using System.IO;
using System.Xml.Linq;
using Xunit;

namespace Device.Controls.Tests;

/// <summary>
/// CameraView.xaml 结构回归：图像框尺寸由 ImageFrame 槽位约束；横向滚动策略与表格自滚动一致。
/// </summary>
public sealed class CameraViewXamlTests
{
    private static readonly XNamespace W =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void XamlFile_ExistsAtKnownPath()
    {
        Assert.True(File.Exists(CameraViewPath), $"未找到 CameraView.xaml：{CameraViewPath}");
    }

    [Fact]
    public void ScrollViewer_DisablesPageHorizontalScroll_KeepsVerticalAuto()
    {
        var root = Load();
        var scroll = root.Descendants(W + "ScrollViewer").First();

        Assert.Equal("Disabled", scroll.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Equal("Auto", scroll.Attribute("VerticalScrollBarVisibility")?.Value);
    }

    [Fact]
    public void ImageFrame_IsNamedAndClipsToBounds()
    {
        var root = Load();
        var frame = root.Descendants(W + "Border")
            .First(b => (string?)b.Attribute(X + "Name") == "ImageFrame");

        Assert.Equal("True", frame.Attribute("ClipToBounds")?.Value);
        Assert.Equal("#101418", frame.Attribute("Background")?.Value);
    }

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

    [Fact]
    public void Image_IsNotGivenFixedWidthOrHeight()
    {
        var root = Load();
        var image = root.Descendants(W + "Image")
            .First(i => ((string?)i.Attribute("Source") ?? string.Empty).Contains("Model.Preview"));

        Assert.Null(image.Attribute("Width"));
        Assert.Null(image.Attribute("Height"));
    }

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
