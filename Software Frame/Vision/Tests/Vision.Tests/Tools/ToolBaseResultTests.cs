// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ToolBaseResultTests、FailingResult、StubParameters、FailingHalconTool、FailingOpenCvTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Threading、Vision.Core.Imaging、Vision.Core.Results、Vision.Tools.Halcon.Internal、Vision.Tools.OpenCv.Internal、Xunit。导入不代表所有类型都会在运行时加载。
using System.Threading;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Tools.Halcon.Internal;
using Vision.Tools.OpenCv.Internal;
using Xunit;

namespace Vision.Tests.Tools;

/// <summary>
/// ToolBaseResultTests 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Trait("Category", "Contract")]
public sealed class ToolBaseResultTests
{
    /// <summary>
    /// 测试相关入口 HalconBase_PreservesFailureReturnedByRunCore；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、False、Equal、NotNull、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void HalconBase_PreservesFailureReturnedByRunCore()
    {
        using var tool = new FailingHalconTool();

        var result = tool.Run(null);

        Assert.False(result.Success);
        Assert.Equal("算法未找到目标", result.Message);
        Assert.NotNull(result.Validation);
        Assert.True(result.Validation!.IsValid);
    }

    /// <summary>
    /// 测试相关入口 OpenCvBase_PreservesFailureReturnedByRunCore；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromBuffer、Run、False、Equal、NotNull；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void OpenCvBase_PreservesFailureReturnedByRunCore()
    {
        using var tool = new FailingOpenCvTool();
        var image = VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0]);

        var result = tool.Run(image);

        Assert.False(result.Success);
        Assert.Equal("算法未找到目标", result.Message);
        Assert.NotNull(result.Validation);
        Assert.True(result.Validation!.IsValid);
    }

    /// <summary>
    /// 测试相关入口 HalconBase_ValidationFailureResetsLastElapsedMs；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、True、False、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void HalconBase_ValidationFailureResetsLastElapsedMs()
    {
        using var tool = new FailingHalconTool();
        tool.Run(null);
        Assert.True(tool.LastElapsedMs > 0);

        tool.Parameters.Invalid = true;
        var result = tool.Run(null);

        Assert.False(result.Success);
        Assert.Equal(0, result.ElapsedMs);
        Assert.Equal(0, tool.LastElapsedMs);
    }

    /// <summary>
    /// 测试相关入口 OpenCvBase_ValidationFailureResetsLastElapsedMs；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromBuffer、Run、True、False、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void OpenCvBase_ValidationFailureResetsLastElapsedMs()
    {
        using var tool = new FailingOpenCvTool();
        var image = VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0]);
        tool.Run(image);
        Assert.True(tool.LastElapsedMs > 0);

        tool.Parameters.Invalid = true;
        var result = tool.Run(image);

        Assert.False(result.Success);
        Assert.Equal(0, result.ElapsedMs);
        Assert.Equal(0, tool.LastElapsedMs);
    }

    /// <summary>
    /// FailingResult 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ToolResult。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class FailingResult : ToolResult;

    /// <summary>
    /// StubParameters 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    private sealed class StubParameters
    {
        public bool Invalid { get; set; }
    }

    /// <summary>
    /// FailingHalconTool 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：HalconToolBase&lt;StubParameters, FailingResult&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class FailingHalconTool : HalconToolBase<StubParameters, FailingResult>
    {
        public override string Name => "FailureStub";

        /// <summary>
        /// 测试相关入口 ValidateCore；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 AddError；具体条件和先后次序见方法体。
        /// </summary>
        protected override ValidationResult ValidateCore(StubParameters parameters) =>
            parameters.Invalid ? new ValidationResult().AddError("INVALID", "参数无效") : new ValidationResult();

        /// <summary>
        /// 测试相关入口 RunCore；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Sleep；具体条件和先后次序见方法体。
        /// </summary>
        protected override FailingResult RunCore(HalconDotNet.HObject? image, StubParameters parameters)
        {
            Thread.Sleep(20);
            return new() { Success = false, Message = "算法未找到目标" };
        }
    }

    /// <summary>
    /// FailingOpenCvTool 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：OpenCvToolBase&lt;StubParameters, FailingResult&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class FailingOpenCvTool : OpenCvToolBase<StubParameters, FailingResult>
    {
        public override string Name => "FailureStub";

        /// <summary>
        /// 测试相关入口 ValidateCore；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 AddError；具体条件和先后次序见方法体。
        /// </summary>
        protected override ValidationResult ValidateCore(StubParameters parameters) =>
            parameters.Invalid ? new ValidationResult().AddError("INVALID", "参数无效") : new ValidationResult();

        /// <summary>
        /// 测试相关入口 RunCore；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Sleep；具体条件和先后次序见方法体。
        /// </summary>
        protected override FailingResult RunCore(VisionImage image, StubParameters parameters)
        {
            Thread.Sleep(20);
            return new() { Success = false, Message = "算法未找到目标" };
        }
    }
}
