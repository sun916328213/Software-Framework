#nullable enable

namespace Vision.Core.Results;

/// <summary>保留QR读取快照与期望文本的精确匹配OK/NG；未读码或文本不符均为NG，不伪装执行错误。</summary>
public sealed class QrTextJudgmentOutput
{
    public QrCodeReadOutput Source
    {
        get;
    }

    public string ExpectedText
    {
        get;
    }

    public bool Passed
    {
        get;
    }

    public QrTextJudgmentOutput(QrCodeReadOutput source, string expectedText, bool passed)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrEmpty(expectedText) || expectedText.Length > 8192
            || passed != (source.State == QrCodeReadState.Decoded && string.Equals(source.Text, expectedText, StringComparison.Ordinal)))
        {
            throw new ArgumentException("QR文本判定必须符合本次读取状态及Ordinal全文匹配规则。");
        }
        Source = source;
        ExpectedText = expectedText;
        Passed = passed;
    }
}
