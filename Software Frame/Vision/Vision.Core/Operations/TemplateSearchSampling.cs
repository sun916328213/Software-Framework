#nullable enable

namespace Vision.Core.Operations;

/// <summary>
/// 明确区分离散姿态采样和原生自动搜索；不把粗网格冒充连续精化，实现不支持时应拒绝。
/// </summary>
public sealed class TemplateSearchSampling
{
    /// <summary>
    /// 显式委托实现的原生金字塔/精化搜索；与离散步长语义互斥。
    /// </summary>
    public bool UsesNativeSearch
    {
        get;
    }

    public static TemplateSearchSampling NativeAutomatic { get; } = new(true);

    /// <summary>
    /// 仅UsesNativeSearch=false时生效；原生模式下的默认值不是实际SDK采样精度。
    /// </summary>
    public double AngleStepDegrees
    {
        get;
    }

    /// <summary>
    /// 仅用于离散模式；不能把原生模式的默认值展示成算法实际缩放步长。
    /// </summary>
    public double ScaleStep
    {
        get;
    }

    private TemplateSearchSampling(bool nativeSearch) : this() => UsesNativeSearch = nativeSearch;

    /// <summary>
    /// 创建明确的离散角度与缩放采样步长。
    /// </summary>
    public TemplateSearchSampling(
        double angleStepDegrees = 5,
        double scaleStep = 0.1)
    {
        if (!double.IsFinite(angleStepDegrees) || angleStepDegrees <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(angleStepDegrees));
        }

        if (!double.IsFinite(scaleStep) || scaleStep <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scaleStep));
        }

        AngleStepDegrees = angleStepDegrees;
        ScaleStep = scaleStep;
    }
}
