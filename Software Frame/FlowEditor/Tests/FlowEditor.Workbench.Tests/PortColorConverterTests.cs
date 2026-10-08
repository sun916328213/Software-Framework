// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PortColorConverterTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Globalization、System.Windows.Media、FlowEditor.Controls.Converters、FlowEditor.Controls.Models、FlowEditor.Core.Models、Xunit。导入不代表所有类型都会在运行时加载。
using System.Globalization;
using System.Windows.Media;
using FlowEditor.Controls.Converters;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// PortColorConverterTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class PortColorConverterTests
{
    /// <summary>
    /// 测试相关入口 Convert_UsesStableSemanticColor；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 IsType、Convert、Equal、FromRgb；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(PortType.Flow, 0x8B, 0x5C, 0xF6)]
    [InlineData(PortType.Boolean, 0xE8, 0x5D, 0x8E)]
    [InlineData(PortType.Integer, 0x71, 0xB8, 0x4B)]
    [InlineData(PortType.Double, 0xB8, 0xC0, 0xC8)]
    [InlineData(PortType.String, 0x58, 0xA9, 0xDE)]
    [InlineData(PortType.Image, 0xF5, 0x9E, 0x0B)]
    [InlineData(PortType.Object, 0x4F, 0x67, 0xC7)]
    [InlineData(PortType.Any, 0x7B, 0x87, 0x94)]
    public void Convert_UsesStableSemanticColor(PortType type, byte red, byte green, byte blue)
    {
        var converter = new PortColorConverter();

        var brush = Assert.IsType<SolidColorBrush>(
            converter.Convert(type, typeof(Brush), new object(), CultureInfo.InvariantCulture));

        Assert.Equal(Color.FromRgb(red, green, blue), brush.Color);
    }

    /// <summary>
    /// 测试相关入口 Convert_UnknownValueFallsBackToAnyColor；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 IsType、Convert、Equal、FromRgb；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Convert_UnknownValueFallsBackToAnyColor()
    {
        var converter = new PortColorConverter();

        var brush = Assert.IsType<SolidColorBrush>(
            converter.Convert(new object(), typeof(Brush), new object(), CultureInfo.InvariantCulture));

        Assert.Equal(Color.FromRgb(0x7B, 0x87, 0x94), brush.Color);
    }
}
