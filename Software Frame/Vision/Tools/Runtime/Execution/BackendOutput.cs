namespace Vision.Tools.Execution;

/// <summary>
/// 仅在内部跨操作分派边界传递结果；发布前须核对实际结果类型、输入来源和空结果状态。
/// Value 不承载参数字典、SDK 对象或旧结果适配器。
/// </summary>
internal sealed record BackendOutput(object Value, bool NoMatch = false);
