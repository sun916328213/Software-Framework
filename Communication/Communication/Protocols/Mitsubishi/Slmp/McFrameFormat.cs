namespace CommunicationNew.Protocols.Mitsubishi;

/// <summary>SLMP/MC 二进制帧格式。</summary>
public enum McFrameFormat
{
    /// <summary>不携带序列号的 QnA 兼容 3E 帧。</summary>
    ThreeE,

    /// <summary>携带序列号、可关联请求响应的 4E 帧。</summary>
    FourE,
}
