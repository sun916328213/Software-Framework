namespace ScriptEditor.Models;

/// <summary>Stable script project data, independent of editor and runtime state.</summary>
public sealed class ScriptProject
{
    public string Name { get; set; } = "新建任务";
    public List<ScriptReference> References { get; set; } = new();
    public List<ScriptProjectItem> Scripts { get; set; } = new();
}

public sealed class ScriptProjectItem
{
    private Guid _scriptId = Guid.NewGuid();
    public Guid ScriptId
    {
        get => _scriptId;
        set => _scriptId = value == Guid.Empty ? Guid.NewGuid() : value;
    }
    public string Title { get; set; } = "新脚本";
    public string Code { get; set; } = string.Empty;
    public List<ScriptReference> References { get; set; } = new();
    public List<int> Breakpoints { get; set; } = new();
}
