using FlowEditor.Nodes;
using System.Windows;
using FlowEditor.Core;

namespace FlowNodes.Script;

/// <summary>
/// 脚本节点：执行 C# 脚本代码。
/// 集成 ScriptEditor 编辑器，支持通过自定义输入端口传递参数到脚本。
///
/// 输入传递方式：通过 ScriptEditorService.SetInputData() 将自定义输入端口的值
/// 作为 ScriptGlobals.Inputs 字典注入脚本上下文，脚本中通过 Inputs["key"] 访问。
/// </summary>
[FlowTool("脚本执行", "CodeBraces", ToolGroup = "脚本", SortIndex = 20, Description = "执行 C# 脚本代码")]
public class ScriptNode : NodeModel
{
    private ScriptEditor.Models.ScriptEditorState _editorState = new();

    /// <summary>
    /// 脚本编辑器状态（编辑器自身的代码与 DLL 引用数据）。
    /// 以整体对象随流程图序列化保存/还原，节点不对外暴露内部代码/引用参数。
    /// </summary>
    public ScriptEditor.Models.ScriptEditorState EditorState
    {
        get => _editorState;
        set => SetProperty(ref _editorState, value ?? new());
    }

    [NodeAction("编辑脚本", Order = 0, IconResource = "CodeBraces")]
    public void EditScript()
    {
        OpenScriptEditor();
    }

  
    /// <summary>
    /// 执行脚本。将自定义输入端口的值收集为 Dictionary，
    /// 通过会话 SetInputData() 注入脚本运行时上下文。
    /// 效果等同于脚本内预定义变量 Inputs["key"]。
    /// 失败语义：编译/运行失败抛出异常（携带脚本输出尾部详情），走容错路径。
    /// </summary>
    public override async Task ExecuteAsync(CancellationToken ct)
    {
        // 空脚本视为无操作成功
        if (string.IsNullOrWhiteSpace(EditorState.Code))
        {
            ScriptResult = null;
            return;
        }

        // P3：运行状态由会话独占持有。节点无编辑器实例，使用一次性独立会话，
        // 运行完即释放，不在运行时上留下长期关联。
        var runtime = ScriptEditor.Services.RoslynScriptRuntime.Shared;
        var session = runtime.CreateDetachedSession(
            Name,
            () => EditorState.Code,
            () => EditorState.References
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => new ScriptEditor.Models.ScriptReference(path, System.IO.Path.GetFileName(path)))
                .ToList());
        try
        {
            session.SetInputData(_BuildInputs());
            var success = await session.RunAsync();
            ScriptResult = session.LastResult;
            // 会话的 Output 由执行线程同步冲刷，RunAsync 返回时值即时完整。
            _lastOutput = session.Output;

            if (!success)
            {
                throw new InvalidOperationException(
                    $"脚本执行失败 [{Name}]：{Truncate(session.Output, 500)}");
            }
        }
        finally
        {
            runtime.ReleaseSession(session);
        }
    }

    /// <summary>
    /// 把自定义输入端口打包为 Dictionary&lt;string, object?&gt;，供脚本 Inputs["portName"] 访问。
    /// </summary>
    private Dictionary<string, object?> _BuildInputs()
    {
        var inputs = new Dictionary<string, object?>();
        foreach (FlowEditor.Models.PortModel port in CustomInputPorts)
            inputs[port.Name] = port.Value;
        return inputs;
    }

    private static string Truncate(string? text, int maxLength)
        => string.IsNullOrWhiteSpace(text) ? "（无输出）"
           : text.Length <= maxLength ? text
           : text[..maxLength] + $"…（已截断，共 {text.Length} 字符）";

    private object? _scriptResult;
    /// <summary>脚本最后一次运行返回的结果（可绑定到输出端口）。</summary>
    public object? ScriptResult
    {
        get => _scriptResult;
        set => SetProperty(ref _scriptResult, value);
    }

    /// <summary>最近一次脚本执行的输出文本（运行态数据，不随流程图序列化）。</summary>
    private string? _lastOutput;
 

   

    /// <summary>
    /// 打开脚本编辑器窗口。
    /// 以普通 WPF 窗口直接承载 ScriptEditorView（窗口 800x600 可缩放、GridSplitter 可拖动），
    /// 传入整个编辑器状态，关闭窗口后整体回写编辑后的代码与 DLL 引用。
    /// </summary>
    private void OpenScriptEditor()
    {
        var editor = new ScriptEditor.Views.ScriptEditorView($"脚本编辑器 - {Name}", EditorState, _BuildInputs(), _lastOutput);

        var window = new Window
        {
            Title = $"脚本编辑器 - {Name}",
            Content = editor,
            Owner = Application.Current?.MainWindow,
            Width = 900,
            Height = 700,
            SizeToContent = SizeToContent.Manual,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Style = Application.Current?.TryFindResource("MaterialDesignWindow") as Style,
        };
        window.ShowDialog();

        var doc = editor.Document;
        if (doc.Code != EditorState.Code
            || doc.References.Count != EditorState.References.Count
            || !doc.References.SequenceEqual(EditorState.References))
        {
            EditorState = doc;
        }
    }
}
