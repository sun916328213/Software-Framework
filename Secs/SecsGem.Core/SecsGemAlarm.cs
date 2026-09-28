namespace SecsGem.Core;

/// <summary>S5F1 报警数据；Code 为 1 表示报警、0 表示复位。</summary>
public sealed record SecsGemAlarm(string Id, string Description, byte Code, bool Enabled = true);
