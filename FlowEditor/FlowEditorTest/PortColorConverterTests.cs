using System.Globalization;
using System.Windows.Media;
using FlowEditor.Converters;
using FlowEditor.Models;
using Xunit;

namespace FlowEditorTest;

public class PortColorConverterTests
{
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
            converter.Convert(type, typeof(Brush), null!, CultureInfo.InvariantCulture));

        Assert.Equal(Color.FromRgb(red, green, blue), brush.Color);
    }

    [Fact]
    public void Convert_UnknownValueFallsBackToAnyColor()
    {
        var converter = new PortColorConverter();

        var brush = Assert.IsType<SolidColorBrush>(
            converter.Convert(new object(), typeof(Brush), null!, CultureInfo.InvariantCulture));

        Assert.Equal(Color.FromRgb(0x7B, 0x87, 0x94), brush.Color);
    }
}
