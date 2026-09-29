using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using ScriptEditor.Contracts;
using ScriptEditor.Models;

namespace ScriptEditor.Services;

/// <summary>Resolves script reference descriptions when the runtime needs assemblies.</summary>
/// <remarks>
/// P5：用户引用经共享 <see cref="MetadataReference"/> 缓存加载
///（按“路径 + 文件写入时间”命中，未命中/文件变化时重建），
/// 多会话引用同一 DLL 时只解析一次；路径去重大小写不敏感。
/// </remarks>
public sealed class RoslynScriptReferenceResolver : IScriptReferenceResolver
{
    private sealed record CacheEntry(DateTime LastWriteTimeUtc, MetadataReference Reference);

    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ScriptReference> Resolve(ScriptProject project, ScriptProjectItem script)
        => project.References.Concat(script.References)
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Path))
            .DistinctBy(reference => reference.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

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
