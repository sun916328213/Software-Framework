// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：TransformPointParameters、TransformPointResult、TransformPointTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Globalization、HalconDotNet、Vision.Core.Geometry、Vision.Core.Results、Vision.Tools.Halcon.Internal。导入不代表所有类型都会在运行时加载。
using System.Globalization;
using HalconDotNet;
using Vision.Core.Geometry;
using Vision.Core.Results;
using Vision.Tools.Halcon.Internal;

namespace Vision.Tools.Halcon.Coordinate;

/// <summary>
/// TransformPointParameters 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// </summary>
public sealed class TransformPointParameters
{
    public Point2D Point { get; set; }
    public string Matrix { get; set; } = "1,0,0|0,1,0|0,0,1";
}
/// <summary>
/// TransformPointResult 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：ToolResult。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class TransformPointResult : ToolResult
{
    public Point2D Point { get; set; }
}
/// <summary>
/// TransformPointTool 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolBase&lt;TransformPointParameters, TransformPointResult&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class TransformPointTool : HalconToolBase<TransformPointParameters, TransformPointResult>
{
    public override string Name => "TransformPoint";
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ParseMatrix。
    /// 正文中的关键协作调用包括 ToArray、Select、Split、Parse、Any；具体条件和先后次序见方法体。
    /// </summary>
    public static double[] ParseMatrix(string matrix)
    {
        var values = matrix.Split(['|', ',']).Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        if (values.Length != 9 || values.Any(v => !double.IsFinite(v)))
            throw new FormatException("需要包含九个有限数值的 3×3 矩阵。");
        return values;
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// 正文中的关键协作调用包括 ParseMatrix、AddError、nameof；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    protected override ValidationResult ValidateCore(TransformPointParameters p)
    {
        var result = new ValidationResult();
        try { ParseMatrix(p.Matrix); }
        catch (Exception e) { result.AddError("MATRIX_INVALID", e.Message, nameof(p.Matrix)); }
        return result;
    }
    /// <summary>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 ParseMatrix、Abs；具体条件和先后次序见方法体。
    /// </summary>
    protected override TransformPointResult RunCore(HObject? image, TransformPointParameters p)
    {
        var m = ParseMatrix(p.Matrix);
        var w = m[6] * p.Point.X + m[7] * p.Point.Y + m[8];
        if (Math.Abs(w) < 1e-12) throw new InvalidOperationException("变换后的齐次坐标为零。");
        return new() { Point = new((m[0]*p.Point.X+m[1]*p.Point.Y+m[2])/w,
                                   (m[3]*p.Point.X+m[4]*p.Point.Y+m[5])/w) };
    }
}
