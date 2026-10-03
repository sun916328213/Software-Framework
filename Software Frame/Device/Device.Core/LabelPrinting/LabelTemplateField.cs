// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LabelTemplateField。成员说明可结合下方具体实现阅读。
namespace Device.Core.LabelPrinting;

/// <summary>
/// 标签模板中的一个命名字段及其当前值。
/// </summary>
/// <param name="Name">模板中的字段名称。</param>
/// <param name="Value">字段当前文本值。</param>
public sealed record LabelTemplateField(string Name, string Value);
