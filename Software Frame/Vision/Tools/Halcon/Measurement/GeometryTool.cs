// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：GeometryOperation、GeometryParameters、GeometryResult、GeometryTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：HalconDotNet、Vision.Core.Geometry、Vision.Core.Results、Vision.Tools.Halcon.Internal。导入不代表所有类型都会在运行时加载。
using HalconDotNet;
using Vision.Core.Geometry;
using Vision.Core.Results;
using Vision.Tools.Halcon.Internal;

namespace Vision.Tools.Halcon.Measurement;

/// <summary>
/// GeometryOperation 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// </summary>
public enum GeometryOperation { Distance, LineIntersection }
/// <summary>
/// GeometryParameters 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// </summary>
public sealed class GeometryParameters
{
    public GeometryOperation Operation { get; set; }
    public Point2D FirstStart { get; set; }
    public Point2D FirstEnd { get; set; }
    public Point2D SecondStart { get; set; }
    public Point2D SecondEnd { get; set; }
}
/// <summary>
/// GeometryResult 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：ToolResult。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class GeometryResult : ToolResult
{
    public double Distance { get; set; }
    public Point2D? Intersection { get; set; }
}
/// <summary>
/// GeometryTool 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolBase&lt;GeometryParameters, GeometryResult&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class GeometryTool : HalconToolBase<GeometryParameters, GeometryResult>
{
    public override string Name => "Geometry";
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// </summary>
    protected override ValidationResult ValidateCore(GeometryParameters p) => new();
    /// <summary>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 DistanceTo、Abs；具体条件和先后次序见方法体。
    /// </summary>
    protected override GeometryResult RunCore(HObject? image, GeometryParameters p)
    {
        if (p.Operation == GeometryOperation.Distance)
            return new() { Distance = p.FirstStart.DistanceTo(p.FirstEnd) };
        var ax = p.FirstEnd.X-p.FirstStart.X; var ay = p.FirstEnd.Y-p.FirstStart.Y;
        var bx = p.SecondEnd.X-p.SecondStart.X; var by = p.SecondEnd.Y-p.SecondStart.Y;
        var cross = ax*by-ay*bx;
        if (Math.Abs(cross)<1e-12) return new() { Success = false, Message = "直线平行或退化。" };
        var t = ((p.SecondStart.X-p.FirstStart.X)*by-(p.SecondStart.Y-p.FirstStart.Y)*bx)/cross;
        return new() { Intersection = new(p.FirstStart.X+t*ax,p.FirstStart.Y+t*ay) };
    }
}
