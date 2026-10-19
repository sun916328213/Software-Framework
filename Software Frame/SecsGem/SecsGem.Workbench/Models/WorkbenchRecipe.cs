using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SecsGem.Workbench.Models;

/// <summary>宿主定义工艺字段和校验规则；不包含 SECS 组包或通信流程。</summary>
public readonly record struct WorkbenchRecipe(
    [property: JsonRequired, Range(1, int.MaxValue)] int Version,
    [property: JsonRequired, Range(1, 1_000_000)] int ExposureUs,
    [property: JsonRequired, Range(0d, 100d)] double Gain,
    [property: JsonRequired, Range(double.Epsilon, 1000d)] double SpeedMmPerSec)
{
    /// <summary>CCODE=1 参数块的业务属性顺序；通信转换仍由 Controls 完成。</summary>
    public static IReadOnlyList<string> FormattedParameterNames { get; } = Array.AsReadOnly(new[]
    {
        nameof(Version), nameof(ExposureUs), nameof(Gain), nameof(SpeedMmPerSec)
    });
}
