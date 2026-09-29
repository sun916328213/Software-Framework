// ─────────────────────────────────────────────────────────────────────────────
// P3 —— NPointsCalibrationTool（Calibration）。
// 迁移源：DPLibrary_Halcon_F48/Tools/Calib/HTool_NPoints.cs。
//
// 命名清理（计划 §P3）：Calib → Calibration（目录名、类名、参数名；旧名只留在迁移源内）。
// 标准化内容：
//   · Parameters：标定点列表（像素 Cx,Cy ↔ 世界 Rx,Ry；旧 CalibPoint 原样建模，改用 Core 友好命名）；
//   · Result：3×3 矩阵串 + X/Y 当量（旧 XEq/YEq 语义：仿射缩放分量，保留 4 位小数）；
//   · Validate：至少 3 点（VectorToHomMat2d 的最低要求）；
//   · Run：VectorToHomMat2d → HomMat2dToAffinePar（与旧一致）。
// ─────────────────────────────────────────────────────────────────────────────

using HalconDotNet;
using Vision.Core.Results;
using Vision.Tools.Halcon.Internal;

namespace Vision.Tools.Halcon.Calibration;

/// <summary>标定点（像素 ↔ 世界）。</summary>
public sealed class CalibrationPoint
{
    /// <summary>世界 X。</summary>
    public double WorldX { get; set; }

    /// <summary>世界 Y。</summary>
    public double WorldY { get; set; }

    /// <summary>像素列（X）。</summary>
    public double PixelX { get; set; }

    /// <summary>像素行（Y）。</summary>
    public double PixelY { get; set; }
}

/// <summary>N 点标定参数。</summary>
public sealed class NPointsCalibrationParameters
{
    /// <summary>标定点（XY 向至少 3 点）。</summary>
    public List<CalibrationPoint> Points { get; set; } = [];
}

/// <summary>N 点标定结果。</summary>
public sealed class NPointsCalibrationResult : ToolResult
{
    /// <summary>相机→工具坐标 3×3 矩阵串。</summary>
    public string Matrix { get; set; } = HalconHelper.IdentityMatrixString;

    /// <summary>X 当量（像素→物理的 X 缩放，4 位小数）。</summary>
    public double ScaleX { get; set; }

    /// <summary>Y 当量。</summary>
    public double ScaleY { get; set; }
}

/// <summary>
/// N 点标定工具（Calibration）：由 ≥3 组“像素↔世界”点对求解仿射矩阵。
/// </summary>
public sealed class NPointsCalibrationTool : HalconToolBase<NPointsCalibrationParameters, NPointsCalibrationResult>
{
    /// <inheritdoc/>
    public override string Name => "NPointsCalibration";

    /// <inheritdoc/>
    protected override ValidationResult ValidateCore(NPointsCalibrationParameters parameters)
    {
        var result = new ValidationResult();
        if (parameters.Points.Count < 3)
            result.AddError("POINTS_TOO_FEW", $"标定至少需要 3 点，实际 {parameters.Points.Count} 点。", nameof(NPointsCalibrationParameters.Points));

        for (var i = 0; i < parameters.Points.Count; i++)
        {
            var point = parameters.Points[i];
            if (double.IsNaN(point.PixelX) || double.IsNaN(point.PixelY) ||
                double.IsNaN(point.WorldX) || double.IsNaN(point.WorldY) ||
                double.IsInfinity(point.PixelX) || double.IsInfinity(point.PixelY) ||
                double.IsInfinity(point.WorldX) || double.IsInfinity(point.WorldY))
                result.AddError("POINT_INVALID", $"第 {i + 1} 个点含非有效数字。", $"{nameof(NPointsCalibrationParameters.Points)}[{i}]");
        }

        return result;
    }

    /// <inheritdoc/>
    protected override NPointsCalibrationResult RunCore(HObject? image, NPointsCalibrationParameters parameters)
    {
        HTuple? px = new HTuple();
        HTuple? py = new HTuple();
        HTuple? wx = new HTuple();
        HTuple? wy = new HTuple();
        HTuple? homMat = null;
        try
        {
            foreach (var point in parameters.Points)
            {
                px = px.TupleConcat(point.PixelX);
                py = py.TupleConcat(point.PixelY);
                wx = wx.TupleConcat(point.WorldX);
                wy = wy.TupleConcat(point.WorldY);
            }

            HOperatorSet.VectorToHomMat2d(px, py, wx, wy, out homMat);
            HOperatorSet.HomMat2dToAffinePar(homMat, out var sx, out var sy, out _, out _, out _, out _);
            try
            {
                return new NPointsCalibrationResult
                {
                    Matrix = HalconHelper.HomMatToString(homMat),
                    ScaleX = Math.Round(sx.D, 4),
                    ScaleY = Math.Round(sy.D, 4),
                };
            }
            finally
            {
                sx.Dispose();
                sy.Dispose();
            }
        }
        finally
        {
            px?.Dispose();
            py?.Dispose();
            wx?.Dispose();
            wy?.Dispose();
            homMat?.Dispose();
        }
    }
}
