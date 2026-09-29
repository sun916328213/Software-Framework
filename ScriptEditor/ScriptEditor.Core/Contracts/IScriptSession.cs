namespace ScriptEditor.Contracts;

public interface IScriptSession
{
    Guid ScriptId { get; }
    bool IsRunning { get; }
    bool IsLooping { get; }
    string Output { get; }
    void Terminate();
}
