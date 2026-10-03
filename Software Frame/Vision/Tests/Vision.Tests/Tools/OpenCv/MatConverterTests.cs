// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MatConverterTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、Vision.Core.Imaging、Vision.Tools.OpenCv.Internal、Xunit。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
using Vision.Core.Imaging;
using Vision.Tools.OpenCv.Internal;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

/// <summary>
/// MatConverterTests 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Trait("Category", "Unit")]
public sealed class MatConverterTests
{
    /// <summary>
    /// 验证 FromVisionImage_IgnoresTrailingBufferBytes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 FromBuffer、FromVisionImage、GetArray、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FromVisionImage_IgnoresTrailingBufferBytes()
    {
        var image = VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [1, 2, 3, 4, 99, 98]);

        using var mat = MatConverter.FromVisionImage(image);
        mat.GetArray(out byte[] pixels);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, pixels);
    }

    /// <summary>
    /// 验证 FromVisionImage_RejectsBufferShortenedAfterConstruction 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 FromBuffer、Throws、FromVisionImage；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FromVisionImage_RejectsBufferShortenedAfterConstruction()
    {
        var image = VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [1, 2, 3, 4]);
        image.Pixels = [1, 2, 3];

        Assert.Throws<ArgumentException>(() => MatConverter.FromVisionImage(image));
    }
}
