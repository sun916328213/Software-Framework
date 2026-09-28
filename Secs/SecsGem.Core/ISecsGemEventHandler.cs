namespace SecsGem.Core;

/// <summary>Host 主动请求业务处理时由宿主提供的回调。</summary>
public interface ISecsGemEventHandler
{
    /// <summary>处理 S2F41；返回 HCACK，0 表示接受。</summary>
    Task<byte> OnRemoteCommandAsync(SecsGemRemoteCommand command);
}

public sealed record SecsGemRemoteCommand(
    string Name,
    IReadOnlyDictionary<string, string> Parameters);
