// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：ScriptNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、FlowEditor.Controls.Interfaces、FlowNodes.Integration、FlowNodes.Presentation.NodeEditing。导入不代表所有类型都会在运行时加载。
using FlowNodes;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Interfaces;
using FlowNodes.Integration;
using FlowNodes.Presentation.NodeEditing;
using ScriptEditor.Core.Contracts;
using ScriptEditor.Core.Models;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;

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
    private readonly Func<IScriptRuntime> _getRuntime;
    private readonly INodeEditorLauncher _editorLauncher;

    /// <summary>兼容既有直接构造和文档物化；默认运行时在首次执行时获取。</summary>
    public ScriptNode() : this(FlowNodeDefaults.GetScriptRuntime, FlowNodesEditorAdapter.Default) { }

    /// <summary>宿主通过已有运行时与编辑 seam 提供协作者。</summary>
    public ScriptNode(IScriptRuntime runtime, INodeEditorLauncher editorLauncher)
        : this(() => runtime, editorLauncher)
    {
        ArgumentNullException.ThrowIfNull(runtime);
    }

    /// <summary>
    /// 初始化 ScriptNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
    /// </summary>
    private ScriptNode(Func<IScriptRuntime> getRuntime, INodeEditorLauncher editorLauncher)
    {
        _getRuntime = getRuntime;
        _editorLauncher = editorLauncher ?? throw new ArgumentNullException(nameof(editorLauncher));
    }

    private ScriptEditor.Core.Models.ScriptEditorState _editorState = new();

    /// <summary>
    /// 脚本编辑器状态（编辑器自身的代码与 DLL 引用数据）。
    /// 以整体对象随流程图序列化保存/还原，节点不对外暴露内部代码/引用参数。
    /// </summary>
    public ScriptEditor.Core.Models.ScriptEditorState EditorState
    {
        get => _editorState;
        set => SetProperty(ref _editorState, value ?? new());
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EditScript。
    /// 正文中的关键协作调用包括 Open；具体条件和先后次序见方法体。
    /// </summary>
    [NodeAction("编辑脚本", Order = 0, IconResource = "CodeBraces")]
    public void EditScript()
    {
        (Service?.NodeEditorLauncher ?? _editorLauncher).Open(this);
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
        var runtime = _getRuntime();
        var session = runtime.CreateDetachedSession(
            Name,
            () => EditorState.Code,
            () => EditorState.References
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => new ScriptEditor.Core.Models.ScriptReference(path, System.IO.Path.GetFileName(path)))
                .ToList());
        try
        {
            session.SetInputData(BuildInputs());
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
    internal Dictionary<string, object?> BuildInputs()
    {
        var inputs = new Dictionary<string, object?>();
        foreach (FlowEditor.Core.Models.PortModel port in CustomInputPorts)
            inputs[port.Name] = port.Value;
        return inputs;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Truncate。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// </summary>
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
    internal string? LastOutput => _lastOutput;
 

   

    /// <summary>
    /// 编辑 adapter 关闭窗口后回写代码与 DLL 引用，保持原有相等比较和通知规则。
    /// </summary>
    internal void ApplyEditorState(ScriptEditorState doc)
    {
        if (doc.Code != EditorState.Code
            || doc.References.Count != EditorState.References.Count
            || !doc.References.SequenceEqual(EditorState.References))
        {
            EditorState = doc;
        }
    }
}
