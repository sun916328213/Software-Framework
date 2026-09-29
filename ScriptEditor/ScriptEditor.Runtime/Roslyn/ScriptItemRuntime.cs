using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using ScriptEditor.Models;

namespace ScriptEditor.Services;

/// <summary>Owns execution engines outside persistent script data until sessions replace them in P3.</summary>
public sealed class ScriptItemRuntime
{
    private sealed class ItemState
    {
        public readonly object Gate = new();
        public ScriptEngine Engine = null!;
        public string[] ReferencePaths = Array.Empty<string>();
        public IReadOnlyList<MetadataReference> References = Array.Empty<MetadataReference>();
        public bool ReferencesLoaded;
    }

    private readonly ConditionalWeakTable<ScriptItem, ItemState> _engines = new();
    private readonly RoslynScriptReferenceResolver _references = new();

    public static ScriptItemRuntime Shared { get; } = new();

    public ScriptEngine GetOrCreate(ScriptItem item) => _engines.GetValue(item, CreateState).Engine;

    public void Release(ScriptItem item)
    {
        if (_engines.TryGetValue(item, out var state)) state.Engine.Terminate();
        _engines.Remove(item);
    }

    private ItemState CreateState(ScriptItem item)
    {
        var state = new ItemState();
        state.Engine = new ScriptEngine(
            () => item.Code,
            () => GetReferences(item, state),
            () => item.Breakpoints.ToArray())
        {
            LoopInterval = item.LoopInterval
        };
        return state;
    }

    private IReadOnlyList<MetadataReference> GetReferences(ScriptItem item, ItemState state)
    {
        var descriptions = item.ScriptReferences;
        var paths = descriptions.Select(reference => reference.Path).ToArray();
        lock (state.Gate)
        {
            if (!state.ReferencesLoaded || !paths.SequenceEqual(state.ReferencePaths, StringComparer.OrdinalIgnoreCase))
            {
                state.References = _references.LoadMetadataReferences(descriptions);
                state.ReferencePaths = paths;
                state.ReferencesLoaded = true;
            }
            return state.References;
        }
    }
}
