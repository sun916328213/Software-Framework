// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：LineGroupParameters、LineGroupResult、LineGroupTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：HalconDotNet、Vision.Core.Results、Vision.Tools.Halcon.Internal。导入不代表所有类型都会在运行时加载。
using HalconDotNet;
using Vision.Core.Results;
using Vision.Tools.Halcon.Internal;

namespace Vision.Tools.Halcon.Measurement;

/// <summary>
/// LineGroupParameters 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// </summary>
public sealed class LineGroupParameters { public List<FindLineParameters> Lines { get; set; } = []; }
/// <summary>
/// LineGroupResult 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：ToolResult。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class LineGroupResult : ToolResult { public List<FindLineResult> Lines { get; set; } = []; }
/// <summary>
/// LineGroupTool 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolBase&lt;LineGroupParameters, LineGroupResult&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class LineGroupTool : HalconToolBase<LineGroupParameters, LineGroupResult>
{
    public override string Name => "LineGroup";
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// 正文中的关键协作调用包括 AddError、nameof、Validate、ToMessage；具体条件和先后次序见方法体。
    /// </summary>
    protected override ValidationResult ValidateCore(LineGroupParameters p)
    {
        var result = new ValidationResult();
        if (p.Lines.Count == 0) result.AddError("LINES_EMPTY", "请配置至少一条测量线。", nameof(p.Lines));
        using var tool = new FindLineTool();
        foreach (var line in p.Lines)
        {
            tool.Parameters = line;
            var validation = tool.Validate();
            if (!validation.IsValid) result.AddError("LINE_INVALID", validation.ToMessage(), nameof(p.Lines));
        }
        return result;
    }
    /// <summary>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 Run、Add；具体条件和先后次序见方法体。
    /// </summary>
    protected override LineGroupResult RunCore(HObject? image, LineGroupParameters p)
    {
        var result = new LineGroupResult();
        using var tool = new FindLineTool();
        foreach (var line in p.Lines)
        {
            tool.Parameters = line;
            var found = tool.Run(image);
            result.Lines.Add(found);
            if (!found.Success) { result.Success = false; result.Message = found.Message; }
        }
        return result;
    }
}
