using Microsoft.CodeAnalysis;
using ScriptEditor.Contracts;
using ScriptEditor.Models;

namespace ScriptEditor.Services;

/// <summary>Resolves script reference descriptions when the runtime needs assemblies.</summary>
public sealed class RoslynScriptReferenceResolver : IScriptReferenceResolver
{
    public IReadOnlyList<ScriptReference> Resolve(ScriptProject project, ScriptProjectItem script)
        => project.References.Concat(script.References)
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Path))
            .DistinctBy(reference => reference.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<MetadataReference> LoadMetadataReferences(IEnumerable<ScriptReference> descriptions)
    {
        var references = new List<MetadataReference>();
        foreach (var path in descriptions.Select(reference => reference.Path)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
            try { references.Add(MetadataReference.CreateFromFile(path)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (BadImageFormatException) { }
            catch (ArgumentException) { }
        }
        return references;
    }
}
