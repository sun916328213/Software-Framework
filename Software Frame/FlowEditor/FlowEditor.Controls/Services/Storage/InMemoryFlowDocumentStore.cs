// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：InMemoryFlowDocumentStore。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Services、System.IO、System.Text.Json、FlowEditor.Controls.Core、FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Services;
using System.IO;
using System.Text.Json;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Interfaces;

namespace FlowEditor.Controls.Services.Storage;

/// <summary>以内存 JSON 副本保存流程文档的无头及测试 adapter。</summary>
public sealed class InMemoryFlowDocumentStore : IFlowDocumentStore
{
    private readonly Dictionary<string, string> _documents = new(StringComparer.Ordinal);

    /// <inheritdoc />
    /// <remarks>
    /// 执行保存操作；对应入口为 Save。
    /// 正文中的关键协作调用包括 ThrowIfNullOrWhiteSpace、ThrowIfNull、Serialize；具体条件和先后次序见方法体。
    /// </remarks>
    public void Save<TDocument>(string path, TDocument document) where TDocument : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);
        _documents[path] = JsonSerializer.Serialize(document, FlowDocumentJson.Options);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行加载操作；对应入口为 Load。
    /// 正文中的关键协作调用包括 ThrowIfNullOrWhiteSpace、TryGetValue、Deserialize；具体条件和先后次序见方法体。
    /// </remarks>
    public TDocument Load<TDocument>(string path) where TDocument : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!_documents.TryGetValue(path, out var json))
            throw new FileNotFoundException("内存流程文档不存在", path);

        return JsonSerializer.Deserialize<TDocument>(json, FlowDocumentJson.Options)
               ?? throw new InvalidDataException("内存流程文档内容为空");
    }
}
