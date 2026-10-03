// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：S6PresentationAdapterTests、ProbeTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Globalization、System.Text.Json、System.Windows、System.Windows.Data、System.Windows.Media、System.Windows.Media.Imaging。导入不代表所有类型都会在运行时加载。
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vision.Controls;
using Vision.Controls.Converters;
using Vision.Controls.Presentation;
using Vision.Core;
using Vision.Core.Geometry;
using Vision.Core.Tools;

namespace Vision.Workbench.Tests.Controls;

/// <summary>
/// S6PresentationAdapterTests 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class S6PresentationAdapterTests(WorkbenchAppFixture host)
{
    /// <summary>
    /// 验证 CoordinateJsonRetainsWpfPointShapeAndValues 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Serialize、Equal、Deserialize、DoubleToInt64Bits；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(12.5, -25.75)]
    [InlineData(double.MaxValue, double.Epsilon)]
    [InlineData(-0.0, -1.0)]
    public void CoordinateJsonRetainsWpfPointShapeAndValues(double x, double y)
    {
        var json = JsonSerializer.Serialize(new Point(x, y));
        Assert.Equal(json, JsonSerializer.Serialize(new Point2D(x, y)));
        var restored = JsonSerializer.Deserialize<Point2D>(json);
        Assert.Equal(BitConverter.DoubleToInt64Bits(x), BitConverter.DoubleToInt64Bits(restored.X));
        Assert.Equal(BitConverter.DoubleToInt64Bits(y), BitConverter.DoubleToInt64Bits(restored.Y));
    }

    /// <summary>
    /// 验证 WpfSynchronizationAllowsWorkerAccessWithTheCollectionLock 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、UseSynchronization、GetDefaultView、GetResult、GetAwaiter；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void WpfSynchronizationAllowsWorkerAccessWithTheCollectionLock()
    {
        var collection = host.Invoke(() =>
        {
            var items = new BulkObservableCollection<int>();
            items.UseSynchronization(new WpfToolPresentation());
            _ = CollectionViewSource.GetDefaultView(items);
            return items;
        });
        Task.Run(() => BindingOperations.AccessCollection(collection, () => collection.Add(42), true))
            .GetAwaiter().GetResult();
        // WPF applies worker notifications asynchronously; drain data binding before clearing.
        Application.Current.Dispatcher.Invoke(() =>
        {
            Assert.Equal(42, Assert.Single(collection));
            Assert.Equal(42, Assert.Single(CollectionViewSource.GetDefaultView(collection).Cast<int>()));
            collection.Dispose();
        }, DispatcherPriority.ContextIdle);
    }

    /// <summary>
    /// 验证 ActualPropertyPanelRendersAndButtonUsesUiAdapter 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、Measure、Arrange、UpdateLayout、Render；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ActualPropertyPanelRendersAndButtonUsesUiAdapter()
    {
        host.Invoke(() =>
        {
            var tool = new ProbeTool { Name = "S6 工具" };
            var panel = new Ctr_PropertyPanel { TargetObject = tool };
            panel.Measure(new Size(600, 600));
            panel.Arrange(new Rect(0, 0, 600, 600));
            panel.UpdateLayout();
            new RenderTargetBitmap(600, 600, 96, 96, PixelFormats.Pbgra32).Render(panel);
            var messages = new List<string>();
            tool.AttachPresentation(new WpfToolPresentation(messages.Add));
            tool.Cmd_BindingPort.OnBtClick(tool, EventArgs.Empty);
            Assert.Equal("S6 工具 端口hhhhh。。。。。。。。", Assert.Single(messages));
            tool.Results.Dispose();
        });
    }

    /// <summary>
    /// 验证 MovedConverterKeepsFormattingParsingAndInvalidInputBehavior 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Convert、Equal、IsType、ConvertBack、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MovedConverterKeepsFormattingParsingAndInvalidInputBehavior()
    {
        var converter = new DoubleArray2DToStringConverter();
        double[][] values = [[1, 2], [3, 4]];
        var text = converter.Convert(values, typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal("1, 2 | 3, 4", text);
        var restored = Assert.IsType<double[][]>(converter.ConvertBack(text, typeof(double[][]), null!, CultureInfo.InvariantCulture));
        Assert.Equal(values[0], restored[0]);
        Assert.Equal(values[1], restored[1]);
        Assert.Null(converter.ConvertBack("invalid", typeof(double[][]), null!, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// ProbeTool 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：Tool。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ProbeTool : Tool
    {
        /// <summary>
        /// 验证 Run 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override void Run() { }
        /// <summary>
        /// 验证 Reset 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override void Reset() { }
        /// <summary>
        /// 验证 Pause 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override void Pause() { }
        /// <summary>
        /// 验证 Config 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override void Config() { }
        /// <summary>
        /// 验证 SaveSnapShot 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override void SaveSnapShot() { }
    }
}
