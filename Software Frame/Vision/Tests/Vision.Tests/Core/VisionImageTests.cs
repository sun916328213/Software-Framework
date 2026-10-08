// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：VisionImageTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core.Imaging、Xunit。导入不代表所有类型都会在运行时加载。
using Vision.Core.Imaging;
using Xunit;

namespace Vision.Tests.Core;

/// <summary>
/// VisionImageTests 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Trait("Category", "Unit")]
public sealed class VisionImageTests
{
    /// <summary>
    /// 测试相关入口 FromBuffer_RejectsStrideShorterThanPixelRow；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、FromBuffer；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FromBuffer_RejectsStrideShorterThanPixelRow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VisionImage.FromBuffer(3, 2, PixelFormat.Mono8, new byte[4], stride: 2));
    }
}
