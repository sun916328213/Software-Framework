// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：ISecsGemEventHandler、SecsGemRemoteCommand。成员说明可结合下方具体实现阅读。
namespace SecsGem.Core;

/// <summary>Host 主动请求业务处理时由宿主提供的回调。</summary>
public interface ISecsGemEventHandler
{
    /// <summary>处理 S2F41；返回 HCACK，0 表示接受。</summary>
    Task<byte> OnRemoteCommandAsync(SecsGemRemoteCommand command);
}

/// <summary>
/// SecsGemRemoteCommand 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// </summary>
public sealed record SecsGemRemoteCommand(
    string Name,
    IReadOnlyDictionary<string, string> Parameters);
