// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：CannyLineParameters、CannyLineResult、CannyLineTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：HalconDotNet、Vision.Core.Geometry、Vision.Core.Results、Vision.Tools.Halcon.Internal。导入不代表所有类型都会在运行时加载。
using HalconDotNet;
using Vision.Core.Geometry;
using Vision.Core.Results;
using Vision.Tools.Halcon.Internal;

namespace Vision.Tools.Halcon.Measurement;

/// <summary>
/// CannyLineParameters 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// </summary>
public sealed class CannyLineParameters
{
    public double Alpha { get; set; } = 1;
    public double Low { get; set; } = 20;
    public double High { get; set; } = 40;
    public double MinimumLength { get; set; } = 20;
}
/// <summary>
/// CannyLineResult 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：ToolResult。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class CannyLineResult : ToolResult { public List<FindLineResult> Lines { get; set; } = []; }
/// <summary>
/// CannyLineTool 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolBase&lt;CannyLineParameters, CannyLineResult&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class CannyLineTool : HalconToolBase<CannyLineParameters, CannyLineResult>
{
    public override string Name => "CannyLine";
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// 正文中的关键协作调用包括 AddError、nameof；具体条件和先后次序见方法体。
    /// </summary>
    protected override ValidationResult ValidateCore(CannyLineParameters p)
    {
        var r = new ValidationResult();
        if (p.Alpha <= 0 || p.Low < 0 || p.High < p.Low || p.MinimumLength < 0)
            r.AddError("CANNY_PARAMETERS", "检查平滑、阈值及最小长度。", nameof(p));
        return r;
    }
    /// <summary>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 ThrowIfNull、EdgesSubPix、SegmentContoursXld、SelectContoursXld、FitLineContourXld；具体条件和先后次序见方法体。
    /// </summary>
    protected override CannyLineResult RunCore(HObject? image, CannyLineParameters p)
    {
        ArgumentNullException.ThrowIfNull(image);
        HOperatorSet.EdgesSubPix(image, out var edges, "canny", p.Alpha, p.Low, p.High);
        using (edges)
        {
            HOperatorSet.SegmentContoursXld(edges, out var segments, "lines", 5, 4, 2);
            using (segments)
            {
                HOperatorSet.SelectContoursXld(segments, out var selected, "contour_length", p.MinimumLength, 1e10, -0.5, 0.5);
                using (selected)
                {
                    HOperatorSet.FitLineContourXld(selected, "tukey", -1, 0, 5, 2,
                        out var r1, out var c1, out var r2, out var c2, out var nr, out var nc, out var distance);
                    using (r1) using (c1) using (r2) using (c2) using (nr) using (nc) using (distance)
                    {
                        var result = new CannyLineResult();
                        for (var i = 0; i < r1.Length; i++)
                            result.Lines.Add(new() { Start = new(c1[i].D,r1[i].D), End = new(c2[i].D,r2[i].D) });
                        return result;
                    }
                }
            }
        }
    }
}
