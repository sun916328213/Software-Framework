using ScriptEditor.Models;

namespace ScriptEditor.Contracts;

public interface IScriptReferenceResolver
{
    IReadOnlyList<ScriptReference> Resolve(ScriptProject project, ScriptProjectItem script);
}
