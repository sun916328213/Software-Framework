#nullable enable

namespace Vision.Core.Results;

/// <summary>
/// 独立候选快照；得分语义必须明确，不保证不同方法/后端的数值可直接比较。
/// </summary>
public sealed class TemplateMatchOutput
{
    /// <summary>
    /// 本次搜索输入帧身份，不是模板帧或模型身份。
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
    /// 当前得分的具体含义，不能据此假定不同评价方式数值等价。
    /// </summary>
    public string ScoreMeaning
    {
        get;
    }

    /// <summary>
    /// 本次有效匹配候选的只读快照。
    /// </summary>
    public IReadOnlyList<TemplateMatchCandidate> Matches
    {
        get;
    }

    /// <summary>
    /// 创建本次匹配结果的独立候选快照。
    /// </summary>
    public TemplateMatchOutput(
        Guid frameId,
        Guid coordinateSpaceId,
        string scoreMeaning,
        IEnumerable<TemplateMatchCandidate> matches)
    {
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty)
        {
            throw new ArgumentException("结果必须注明帧和坐标空间。");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(scoreMeaning);
        ArgumentNullException.ThrowIfNull(matches);
        var snapshot = matches.ToArray();
        if (snapshot.Any(match => match == null))
        {
            throw new ArgumentException("候选不能包含空值。", nameof(matches));
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        ScoreMeaning = scoreMeaning;
        Matches = Array.AsReadOnly(snapshot);
    }
}
