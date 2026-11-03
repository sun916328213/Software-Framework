#nullable enable

namespace Vision.Core.Results;

/// <summary>本次返回候选集的成功解码计数OK/NG，不把未解码候选或相同文本的不同位置混为一谈。</summary>
public sealed class QrCountJudgmentOutput
{
    public QrCodeReadManyOutput Source
    {
        get;
    }

    public int Minimum
    {
        get;
    }

    public int Maximum
    {
        get;
    }

    public bool Passed
    {
        get;
    }

    public int ActualCount => Source.DecodedCount;

    public QrCountJudgmentOutput(QrCodeReadManyOutput source, int minimum, int maximum, bool passed)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (minimum < 0 || maximum < minimum || passed != (source.DecodedCount >= minimum && source.DecodedCount <= maximum))
        {
            throw new ArgumentException("多QR成功解码计数判定必须符合本次候选快照与闭区间。");
        }
        Source = source;
        Minimum = minimum;
        Maximum = maximum;
        Passed = passed;
    }
}
