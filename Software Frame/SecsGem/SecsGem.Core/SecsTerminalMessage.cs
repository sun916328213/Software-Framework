namespace SecsGem.Core;

/// <summary>已接收的终端通知快照；不持有 SDK Item。Id 只在当前 Runtime 实例内有意义。</summary>
public sealed record SecsTerminalMessage(long Id, byte TerminalId, string Command, string Text, DateTimeOffset ReceivedAt);
