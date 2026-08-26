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
    /// 通过 ScriptEditorService.SetInputData() 注入脚本运行时上下文。
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

        var inputs = new Dictionary<string, object?>();
        foreach (FlowEditor.Models.PortModel port in CustomInputPorts)
        {
            inputs[port.Name] = port.Value;
        }

        var service = new ScriptEditor.Services.ScriptEditorService();
        service.SetText(EditorState.Code);
        service.SetInputData(inputs);
        foreach (var path in EditorState.References)
            service.AddReference(path);
        var success = await service.RunAsync();
        ScriptResult = service.LastScriptResult;

        if (!success)
        {
            throw new InvalidOperationException(
                $"脚本执行失败 [{Name}]：{Truncate(service.Model.OutputText, 500)}");
        }
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
 

   

    /// <summary>
    /// 打开脚本编辑器窗口。
    /// 以普通 WPF 窗口直接承载 ScriptEditorView（窗口 800x600 可缩放、GridSplitter 可拖动），
    /// 传入整个编辑器状态，关闭窗口后整体回写编辑后的代码与 DLL 引用。
    /// </summary>
    private void OpenScriptEditor()
    {
        var inputs = new Dictionary<string, object?>();
        foreach (var port in CustomInputPorts)
        {
            inputs[port.Name] = port.Value;
        }

        var editor = new ScriptEditor.Views.ScriptEditorView($"脚本编辑器 - {Name}", EditorState, inputs);

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
