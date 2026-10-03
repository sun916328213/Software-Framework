// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：RoslynScriptReferenceResolver、CacheEntry。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Concurrent、Microsoft.CodeAnalysis、ScriptEditor.Core.Contracts、ScriptEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using ScriptEditor.Core.Contracts;
using ScriptEditor.Core.Models;

namespace ScriptEditor.Runtime.Services;

/// <summary>Resolves script reference descriptions when the runtime needs assemblies.</summary>
/// <remarks>
/// P5：用户引用经共享 <see cref="MetadataReference"/> 缓存加载
///（按“路径 + 文件写入时间”命中，未命中/文件变化时重建），
/// 多会话引用同一 DLL 时只解析一次；路径去重大小写不敏感。
/// </remarks>
public sealed class RoslynScriptReferenceResolver : IScriptReferenceResolver
{
    /// <summary>
    /// CacheEntry 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
    /// </summary>
    private sealed record CacheEntry(DateTime LastWriteTimeUtc, MetadataReference Reference);

    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Resolve。
    /// 正文中的关键协作调用包括 ToArray、DistinctBy、Where、Concat、IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// </summary>
    public IReadOnlyList<ScriptReference> Resolve(ScriptProject project, ScriptProjectItem script)
        => project.References.Concat(script.References)
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Path))
            .DistinctBy(reference => reference.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// 执行加载操作；对应入口为 LoadMetadataReferences。
    /// 正文中的关键协作调用包括 Select、IsNullOrWhiteSpace、Add、GetOrLoad；具体条件和先后次序见方法体。
    /// </summary>
    public IReadOnlyList<MetadataReference> LoadMetadataReferences(IEnumerable<ScriptReference> descriptions)
    {
        var references = new List<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in descriptions.Select(reference => reference.Path))
        {
            if (string.IsNullOrWhiteSpace(path) || !seen.Add(path)) continue;
            var reference = GetOrLoad(path);
            if (reference != null)
                references.Add(reference);
        }
        return references;
    }

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetOrLoad。
    /// 正文中的关键协作调用包括 GetFullPath、Exists、GetLastWriteTimeUtc、TryGetValue、CreateFromFile；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private static MetadataReference? GetOrLoad(string path)
    {
        string fullPath;
        DateTime lastWrite;
        try
        {
            fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) return null;
            lastWrite = File.GetLastWriteTimeUtc(fullPath);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (ArgumentException) { return null; }

        if (Cache.TryGetValue(fullPath, out var entry) && entry.LastWriteTimeUtc == lastWrite)
            return entry.Reference;

        MetadataReference reference;
        try { reference = MetadataReference.CreateFromFile(fullPath); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (BadImageFormatException) { return null; }
        catch (ArgumentException) { return null; }

        Cache[fullPath] = new CacheEntry(lastWrite, reference);
        return reference;
    }
}
