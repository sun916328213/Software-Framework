// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowChartContainerDocumentModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core.Models;
namespace FlowEditor.Controls.Models;

/// <summary>
/// 多流程容器文档模型（序列化根对象）。
/// 包含多个流程图的完整数据，用于容器级别的保存/另存为/打开。
/// </summary>
public class FlowChartContainerDocumentModel
{
    /// <summary>文件格式版本号。</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>所有流程图文档。</summary>
    public List<FlowChartDocumentModel> Flows { get; set; } = new();
}
