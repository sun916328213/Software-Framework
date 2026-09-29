using System.IO;
using System.Text.Json;
using FlowEditor.Core;
using FlowEditor.Interfaces;

namespace FlowEditor.Services.Storage;

/// <summary>使用共享 JSON 格式读写 `.flow` 与 `.flows` 文件的 adapter。</summary>
public sealed class JsonFlowDocumentStore : IFlowDocumentStore
{
    /// <inheritdoc />
    public void Save<TDocument>(string path, TDocument document) where TDocument : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, JsonSerializer.Serialize(document, FlowDocumentJson.Options));
    }

    /// <inheritdoc />
    public TDocument Load<TDocument>(string path) where TDocument : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("流程文档不存在", path);

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TDocument>(json, FlowDocumentJson.Options)
               ?? throw new InvalidDataException("流程文档内容为空");
    }
}
