#nullable enable

namespace Vision.Core.Results;

/// <summary>
/// 类别标识不等同数组下标，置信度范围由统一分类契约约束为[0,1]。
/// </summary>
public sealed record AiClassPrediction
{
    /// <summary>
    /// 类别的稳定标识，不等同于结果数组下标。
    /// </summary>
    public string ClassId
    {
        get;
    }

    /// <summary>
    /// 匹配或分类得分，范围为零到一。
    /// </summary>
    public double Score
    {
        get;
    }

    /// <summary>
    /// 创建一个分类预测，校验类别标识及得分。
    /// </summary>
    public AiClassPrediction(string classId, double score)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classId);
        if (!double.IsFinite(score) || score < 0 || score > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(score));
        }

        ClassId = classId;
        Score = score;
    }
}

/// <summary>
/// 本次图像分类结果，实际执行设备由引擎选择值说明。
/// 引擎发布时保证请求内数量/最低分数、Ordinal唯一类别ID及非升序分数；同分可保留，不宣称唯一最优或校准概率。
/// </summary>
public sealed class AiClassificationOutput
{
    /// <summary>
    /// 本次推理输入帧身份，不是模型或设备身份。
    /// </summary>
    public Guid FrameId
    {
        get;
    }

    /// <summary>
    /// 本次结果所使用的像素坐标空间标识。
    /// </summary>
    public Guid CoordinateSpaceId
    {
        get;
    }

    /// <summary>
    /// 本次分类预测的只读快照。
    /// </summary>
    public IReadOnlyList<AiClassPrediction> Predictions
    {
        get;
    }

    /// <summary>
    /// 创建本次分类结果的独立预测快照。
    /// </summary>
    public AiClassificationOutput(
        Guid frameId,
        Guid coordinateSpaceId,
        IEnumerable<AiClassPrediction> predictions)
    {
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty)
        {
            throw new ArgumentException("结果必须注明帧和坐标空间。");
        }

        ArgumentNullException.ThrowIfNull(predictions);
        var snapshot = predictions.ToArray();
        if (snapshot.Any(prediction => prediction == null))
        {
            throw new ArgumentException("分类项不能包含空值。", nameof(predictions));
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        Predictions = Array.AsReadOnly(snapshot);
    }
}
