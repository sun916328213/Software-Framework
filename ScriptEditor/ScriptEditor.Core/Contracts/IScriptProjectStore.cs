using ScriptEditor.Models;

namespace ScriptEditor.Contracts;

public interface IScriptProjectStore
{
    Task<ScriptProject> LoadAsync(string path, CancellationToken cancellationToken = default);
    Task SaveAsync(string path, ScriptProject project, CancellationToken cancellationToken = default);
}
