using ScriptEditor.Models;

namespace ScriptEditor.Contracts;

public sealed record ScriptExecutionResult(bool Success, string Output, string Status);

public interface IScriptRuntime
{
    Task<ScriptExecutionResult> ExecuteAsync(ScriptProjectItem script, CancellationToken cancellationToken = default);
    Task<bool> CompileAsync(ScriptProjectItem script, CancellationToken cancellationToken = default);
    void Terminate(Guid scriptId);
}
