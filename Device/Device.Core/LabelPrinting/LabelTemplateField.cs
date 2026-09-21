namespace Device.Core.LabelPrinting;

/// <summary>
/// 标签模板中的一个命名字段及其当前值。
/// </summary>
/// <param name="Name">模板中的字段名称。</param>
/// <param name="Value">字段当前文本值。</param>
public sealed record LabelTemplateField(string Name, string Value);
