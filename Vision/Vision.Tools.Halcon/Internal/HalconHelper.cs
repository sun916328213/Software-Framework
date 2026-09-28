// ─────────────────────────────────────────────────────────────────────────────
// P3 —— HALCON 重复 helper 收口（计划 §P3「抽离 Tool 内重复的 HALCON helper 到 Internal」）。
//
// 收口来源（迁移源 DPLibrary_Halcon_F48）：
//   · HTool_Blob / HTool_TemplateMatch 的 SearchRegions 合并（Union2 循环）；
//   · MixtrueEx.ToMixtrueStr / ToHMixtrue（矩阵串 "a,b,c|d,e,f|g,h,i"）；
//   · HTool_Fixtrue 的单位矩阵默认值 "1, 0, 0, 0, 1, 0, 0, 0, 1"；
//   · 各 Tool finally 里的逐个 Dispose。
//
// ⛔ 暂不建立 Vision.Halcon（计划 §12 明确不做）；确有复用才拆。
// ─────────────────────────────────────────────────────────────────────────────

using System.Globalization;
using HalconDotNet;

namespace Vision.Tools.Halcon.Internal;

/// <summary>
/// HALCON Tool 内部共享 helper（纯搬运 + 命名清理，不改算法行为）。
/// </summary>
public static class HalconHelper
{
    /// <summary>单位矩阵串（与旧 <c>HTool_Fixtrue.Matrix</c> 默认值等价，格式已统一为该文件的 <c>|</c> 分隔）。</summary>
    public const string IdentityMatrixString = "1,0,0|0,1,0|0,0,1";

    /// <summary>
    /// 合并多个搜索区域（旧 Blob/TemplateMatch 每次 Run 开头的 Union2 循环）。
    /// </summary>
    /// <param name="regions">待合并区域（调用方保留所有权）。</param>
    /// <returns>合并后的新区域；输入为空时返回 null（调用方按全图处理）。</returns>
    public static HObject? UnionAll(IReadOnlyList<HObject> regions)
    {
        if (regions == null || regions.Count == 0)
            return null;

        HObject merged = regions[0].Clone();
        try
        {
            for (var i = 1; i < regions.Count; i++)
            {
                HOperatorSet.Union2(merged, regions[i], out var tmp);
                merged.Dispose();
                merged = tmp;
            }

            return merged;
        }
        catch
        {
            merged.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 把 3×3 矩阵串解析为 9 个 double（兼容旧两种写法：逗号/竖线分隔、小数/整数）。
    /// </summary>
    /// <exception cref="FormatException">格式不对。</exception>
    public static double[] ParseMatrixString(string matrix)
    {
        if (!TryParseMatrixString(matrix, out var values, out var error))
            throw new FormatException(error);
        return values!;
    }

    /// <summary>尝试解析矩阵串（供 Validate 使用，不抛）。</summary>
    public static bool TryParseMatrixString(string? matrix, out double[]? values, out string error)
    {
        values = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(matrix))
        {
            error = "矩阵串为空。";
            return false;
        }

        var parts = matrix.Split(['|', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 9)
        {
            error = $"矩阵串应含 9 个数字，实际 {parts.Length} 个：{matrix}";
            return false;
        }

        var parsed = new double[9];
        for (var i = 0; i < 9; i++)
        {
            if (!double.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed[i]))
            {
                error = $"矩阵串第 {i + 1} 个数字无法解析：{parts[i]}";
                return false;
            }
        }

        values = parsed;
        return true;
    }

    /// <summary>把 9 个 double 格式化为标准矩阵串（3 位小数，<c>|</c> 分行）。</summary>
    public static string ToMatrixString(double[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length != 9)
            throw new ArgumentException("矩阵必须恰好 9 个数字。", nameof(values));
        return string.Join("|",
            string.Join(",", values[0..3].Select(v => (v + 0.0).ToString("F3", CultureInfo.InvariantCulture))),
            string.Join(",", values[3..6].Select(v => (v + 0.0).ToString("F3", CultureInfo.InvariantCulture))),
            string.Join(",", values[6..9].Select(v => (v + 0.0).ToString("F3", CultureInfo.InvariantCulture))));
    }

    /// <summary>是否为单位矩阵串（Blob 的仿射分支用它决定是否跳过 AffineTransRegion）。</summary>
    public static bool IsIdentityMatrix(string? matrix)
    {
        if (!TryParseMatrixString(matrix, out var values, out _))
            return false;
        var identity = new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
        for (var i = 0; i < 9; i++)
        {
            if (Math.Abs(values![i] - identity[i]) > 1e-9)
                return false;
        }

        return true;
    }

    /// <summary>把 HTuple 同态矩阵转为标准矩阵串（旧 MixtrueEx.ToMixtrueStr 的搬运）。</summary>
    public static string HomMatToString(HTuple homMat)
    {
        ArgumentNullException.ThrowIfNull(homMat);
        return string.Join("|", homMat.DArr.Select((v, i) => new { v, i })
            .GroupBy(x => x.i / 3)
            .Select(g => string.Join(",", g.Select(x => x.v.ToString("F3", CultureInfo.InvariantCulture)))));
    }

    /// <summary>标准矩阵串转 HTuple（旧 MixtrueEx.ToHMixtrue 的搬运）。</summary>
    public static HTuple MatrixStringToHomMat(string matrix) => new(ParseMatrixString(matrix));

    /// <summary>阈值区间合法性（Blob/FindLine 共用；纯逻辑，无 Runtime 依赖，可进普通 CI）。</summary>
    public static bool IsValidThresholdRange(int min, int max, out string error)
    {
        error = string.Empty;
        if (min < 0 || min > 255)
        {
            error = $"最小灰度 {min} 超出 0~255。";
            return false;
        }

        if (max < 0 || max > 255)
        {
            error = $"最大灰度 {max} 超出 0~255。";
            return false;
        }

        if (min > max)
        {
            error = $"最小灰度 {min} 大于最大灰度 {max}。";
            return false;
        }

        return true;
    }
}
