using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace ScriptEditor.Models;

/// <summary>Script data and temporarily retained bindable state for the P3 session migration.</summary>
public class ScriptItem : ScriptStateBase
{
    private Guid _scriptId = Guid.NewGuid();
    private string _title = "新脚本";
    private string _code = string.Empty;
    private string _itemStatus = "就绪";
    private int _caretOffset;
    private double _editorScrollOffset;
    private double _scrollOffset;
    private bool _isEditorInitialized;

    /// <summary>Persistent identity; legacy task files without an ID receive one on load.</summary>
    public Guid ScriptId
    {
        get => _scriptId;
        set => SetProperty(ref _scriptId, value == Guid.Empty ? Guid.NewGuid() : value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    /// <summary>Legacy .taskproj reference paths; retained until the versioned store migration.</summary>
    public List<string> References { get; set; } = new();

    [JsonIgnore]
    public IReadOnlyList<string> ReferencePaths => References?.ToArray() ?? Array.Empty<string>();

    [JsonIgnore]
    public IReadOnlyList<ScriptReference> ScriptReferences => ReferencePaths
        .Select(path => new ScriptReference(path, path[(path.LastIndexOfAny(['\\', '/']) + 1)..]))
        .ToArray();

    public void SetReferences(IEnumerable<string>? paths)
    {
        References = paths?.Where(path => !string.IsNullOrWhiteSpace(path)).ToList() ?? new List<string>();
    }

    public ObservableCollection<int> Breakpoints { get; set; } = new();

    [JsonIgnore]
    public string ItemStatus
    {
        get => _itemStatus;
        set => SetProperty(ref _itemStatus, value);
    }

    [JsonIgnore]
    public int CaretOffset
    {
        get => _caretOffset;
        set => SetProperty(ref _caretOffset, value);
    }

    [JsonIgnore]
    public double EditorScrollOffset
    {
        get => _editorScrollOffset;
        set => SetProperty(ref _editorScrollOffset, value);
    }

    [JsonIgnore]
    public double ScrollOffset
    {
        get => _scrollOffset;
        set => SetProperty(ref _scrollOffset, value);
    }

    [JsonIgnore]
    public bool IsEditorInitialized
    {
        get => _isEditorInitialized;
        set => SetProperty(ref _isEditorInitialized, value);
    }

    public ScriptProjectItem ToProjectItem() => new()
    {
        ScriptId = ScriptId,
        Title = Title,
        Code = Code,
        References = ScriptReferences.ToList(),
        Breakpoints = Breakpoints.ToList()
    };
}
