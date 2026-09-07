namespace FlowEditor.Models;

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
