namespace CommunicationNew.Protocols.Mitsubishi;

/// <summary>通过帧头、长度、关联号和结束码校验的 MC 响应。</summary>
public sealed record McResponse(ReadOnlyMemory<byte> Data);
