namespace CommunicationNew.Protocols.Mitsubishi;

/// <summary>表示 MC 帧格式错误或 PLC 返回非零结束码。</summary>
public sealed class McProtocolException : IOException
{
    /// <summary>使用错误消息创建协议异常。</summary>
    public McProtocolException(string message) : base(message)
    {
    }

    /// <summary>PLC 返回的结束码；本地帧格式错误时为空。</summary>
    public ushort? EndCode { get; init; }
}
