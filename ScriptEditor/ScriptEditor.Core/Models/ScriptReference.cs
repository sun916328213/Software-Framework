namespace ScriptEditor.Models;

public enum ScriptReferenceKind
{
    AssemblyFile,
    FrameworkAssembly
}

/// <summary>A reference described without loading Roslyn or accessing the file system.</summary>
public sealed record ScriptReference(string Path, string Name, ScriptReferenceKind Kind = ScriptReferenceKind.AssemblyFile);
