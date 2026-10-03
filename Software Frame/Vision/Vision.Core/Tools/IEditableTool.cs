// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：IEditableTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core.Results。导入不代表所有类型都会在运行时加载。
using Vision.Core.Results;

namespace Vision.Core.Tools;

/// <summary>A persisted tool with editable parameters and explicit validation.</summary>
/// <remarks>
/// IEditableTool 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：ITool。具体约束按这些契约及各成员实现解释。
/// </remarks>
public interface IEditableTool : ITool
{
    object EditableParameters { get; }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Validate。
    /// </summary>
    ValidationResult Validate();
}
