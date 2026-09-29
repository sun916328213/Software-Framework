using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ScriptEditor.Models;

/// <summary>
/// 脚本数据对象：只描述「脚本是什么」，可独立序列化。
/// </summary>
/// <remarks>
/// <para>P3 起本类型**不再持有任何运行状态**（IsRunning / Output / StatusText / ExecutionTime 等），
/// 这些状态由 <see cref="Contracts.IScriptSession"/> 独占持有，关联关系由
/// <see cref="Contracts.IScriptRuntime"/> 维护。</para>
/// <para>此处保留的 CaretOffset / EditorScrollOffset / ScrollOffset / IsEditorInitialized
/// 是**编辑器视图状态**（按标签页记忆光标与滚动位置），属于 Controls 层关注点，
/// 均标注 <see cref="JsonIgnoreAttribute"/>，不参与持久化。</para>
/// </remarks>
public class ScriptItem : ObservableObject
{
    private Guid _scriptId = Guid.NewGuid();
    private string _title = "新脚本";
    private string _code = string.Empty;
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

    /// <summary>编辑器视图状态：光标偏移（按脚本记忆，不持久化）。</summary>
    [JsonIgnore]
    public int CaretOffset
    {
        get => _caretOffset;
        set => SetProperty(ref _caretOffset, value);
    }

    /// <summary>编辑器视图状态：代码编辑器垂直滚动偏移（不持久化）。</summary>
    [JsonIgnore]
    public double EditorScrollOffset
    {
        get => _editorScrollOffset;
        set => SetProperty(ref _editorScrollOffset, value);
    }

    /// <summary>编辑器视图状态：输出面板垂直滚动偏移（不持久化）。</summary>
    [JsonIgnore]
    public double ScrollOffset
    {
        get => _scrollOffset;
        set => SetProperty(ref _scrollOffset, value);
    }

    /// <summary>编辑器视图状态：该脚本的编辑器是否已初始化（不持久化）。</summary>
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
