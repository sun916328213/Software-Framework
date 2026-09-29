using System.IO;
using System.Text.Json;
using FlowEditor.Core;
using FlowEditor.Interfaces;

namespace FlowEditor.Services.Storage;

/// <summary>以内存 JSON 副本保存流程文档的无头及测试 adapter。</summary>
public sealed class InMemoryFlowDocumentStore : IFlowDocumentStore
{
    private readonly Dictionary<string, string> _documents = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Save<TDocument>(string path, TDocument document) where TDocument : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);
        _documents[path] = JsonSerializer.Serialize(document, FlowDocumentJson.Options);
    }

    /// <inheritdoc />
    public TDocument Load<TDocument>(string path) where TDocument : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!_documents.TryGetValue(path, out var json))
            throw new FileNotFoundException("内存流程文档不存在", path);

        return JsonSerializer.Deserialize<TDocument>(json, FlowDocumentJson.Options)
               ?? throw new InvalidDataException("内存流程文档内容为空");
    }
}
