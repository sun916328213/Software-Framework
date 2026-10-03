// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：FixtureParameters、FixtureResult、FixtureTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：HalconDotNet、Vision.Core.Geometry、Vision.Core.Results、Vision.Tools.Halcon.Internal。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P3 —— FixtureTool（Coordinate）。
// 迁移源：DPLibrary_Halcon_F48/Tools/Fixtrue/HTool_Fixtrue.cs。
//
// 命名清理（计划 §P3）：Fixtrue → Fixture（含目录名、类名；旧拼写只留在迁移源内）。
// 标准化内容：
//   · Parameters：基准点/目标点/旋转/缩放（旧 BasePoint/TransforPoint/TransforR/TransforScale）；
//   · Result：矩阵串 + Pose2D（旧 HMixtrueResult 只暴露 HTuple，新结果用稳定类型）；
//   · Validate：缩放必须为正；
//   · Run：HomMat2dIdentity → Scale → VectorAngleToRigid → Compose（与旧一致）；
//   · 纯数学 BuildPose：用 Core Pose2D.Compose 复刻“先缩放、再刚体”的合成语义，
//     供普通 CI 在无 HALCON Runtime 时验证数值（HALCON 算子路径仍是正式执行路径）。
// ─────────────────────────────────────────────────────────────────────────────

using HalconDotNet;
using Vision.Core.Geometry;
using Vision.Core.Results;
using Vision.Tools.Halcon.Internal;

namespace Vision.Tools.Halcon.Coordinate;

/// <summary>Fixture 运行参数。</summary>
public sealed class FixtureParameters
{
    /// <summary>基准中心点（图像坐标：X=列，Y=行）。</summary>
    public Point2D BasePoint { get; set; }

    /// <summary>转换后中心点（图像坐标：X=列，Y=行）。</summary>
    public Point2D TargetPoint { get; set; }

    /// <summary>旋转角（度）。</summary>
    public double RotationDeg { get; set; }

    /// <summary>缩放（必须为正；1 表示不缩放）。</summary>
    public double Scale { get; set; } = 1;
}

/// <summary>Fixture 结果（矩阵串格式与旧 <c>ToMixtrueStr</c> 一致："a,b,c|d,e,f|g,h,i"）。</summary>
public sealed class FixtureResult : ToolResult
{
    /// <summary>3×3 齐次矩阵串。</summary>
    public string Matrix { get; set; } = HalconHelper.IdentityMatrixString;

    /// <summary>刚体部分（缩放 folds 进矩阵，Pose 只表达旋转+平移）。</summary>
    public Pose2D Pose { get; set; }
}

/// <summary>
/// Fixture 工具（Coordinate）：由一对标定点 + 旋转/缩放求解坐标系变换。
/// </summary>
public sealed class FixtureTool : HalconToolBase<FixtureParameters, FixtureResult>
{
    /// <inheritdoc/>
    public override string Name => "Fixture";

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// 正文中的关键协作调用包括 IsNaN、IsInfinity、AddError、nameof；具体条件和先后次序见方法体。
    /// </remarks>
    protected override ValidationResult ValidateCore(FixtureParameters parameters)
    {
        var result = new ValidationResult();
        if (double.IsNaN(parameters.RotationDeg) || double.IsInfinity(parameters.RotationDeg))
            result.AddError("ROTATION_INVALID", "旋转角不是有效数字。", nameof(FixtureParameters.RotationDeg));
        if (!(parameters.Scale > 0) || double.IsNaN(parameters.Scale) || double.IsInfinity(parameters.Scale))
            result.AddError("SCALE_INVALID", $"缩放必须为正数，实际 {parameters.Scale}。", nameof(FixtureParameters.Scale));
        return result;
    }

    /// <summary>
    /// 纯数学路径：不依赖 HALCON Runtime，用于普通 CI 的数值验证。
    /// 语义与 <see cref="RunCore"/> 的 HALCON 路径一致：先绕原点缩放，再做刚体变换。
    /// </summary>
    public static FixtureResult BuildPose(FixtureParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (!(parameters.Scale > 0))
            throw new ArgumentOutOfRangeException(nameof(parameters.Scale), "缩放必须为正数。");

        // 刚体部分：base → target 的旋转+平移（与 vector_angle_to_rigid 一致：绕原点旋转再平移）。
        var rigid = new Pose2D(
            parameters.TargetPoint.X - (parameters.BasePoint.X * Math.Cos(parameters.RotationDeg * Math.PI / 180.0)
                - parameters.BasePoint.Y * Math.Sin(parameters.RotationDeg * Math.PI / 180.0)),
            parameters.TargetPoint.Y - (parameters.BasePoint.X * Math.Sin(parameters.RotationDeg * Math.PI / 180.0)
                + parameters.BasePoint.Y * Math.Cos(parameters.RotationDeg * Math.PI / 180.0)),
            parameters.RotationDeg);

        // 先缩放 S、再刚体 R：M = R ∘ S。缩放在原点进行，故平移不受缩放影响。
        var s = parameters.Scale;
        var rad = rigid.AngleRad;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var matrix = new double[]
        {
            s * cos, -s * sin, rigid.X,
            s * sin, s * cos, rigid.Y,
            0, 0, 1,
        };

        return new FixtureResult
        {
            Success = true,
            Matrix = HalconHelper.ToMatrixString(matrix),
            Pose = rigid,
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 HomMat2dIdentity、HomMat2dScale、Dispose、VectorAngleToRigid、HomMat2dCompose；具体条件和先后次序见方法体。
    /// </remarks>
    protected override FixtureResult RunCore(HObject? image, FixtureParameters parameters)
    {
        // image 不参与计算（与旧 HTool_Fixtrue.Run 一致：纯矩阵工具），保留参数位以符合统一 Run 形状。
        HTuple? homScale = null;
        HTuple? homRigid = null;
        HTuple? homComposed = null;
        try
        {
            HOperatorSet.HomMat2dIdentity(out homScale);
            HOperatorSet.HomMat2dScale(homScale, parameters.Scale, parameters.Scale, 0, 0, out var scaled);
            homScale.Dispose();
            homScale = scaled;

            HOperatorSet.VectorAngleToRigid(
                parameters.BasePoint.Y, parameters.BasePoint.X, 0,
                parameters.TargetPoint.Y, parameters.TargetPoint.X,
                parameters.RotationDeg * Math.PI / 180.0, out homRigid);
            HOperatorSet.HomMat2dCompose(homRigid, homScale, out homComposed);

            var pure = BuildPose(parameters);
            return new FixtureResult
            {
                Matrix = HalconHelper.HomMatToString(homComposed),
                Pose = pure.Pose,
            };
        }
        finally
        {
            homScale?.Dispose();
            homRigid?.Dispose();
            homComposed?.Dispose();
        }
    }
}
