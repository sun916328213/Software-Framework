// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowDocumentSession。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Services、System.Collections.ObjectModel、System.Text.Json、FlowEditor.Controls.Core、FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Services;
using System.Collections.ObjectModel;
using System.Text.Json;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;

namespace FlowEditor.Controls.Services.Documents;

/// <summary>
/// 管理单个画布文档的保存、加载和撤销快照事务。
/// 节点生命周期仍由画布会话提供，文档模块保证映射、存储、状态更新和失败反馈保持一致。
/// </summary>
internal sealed class FlowDocumentSession
{
    private readonly FlowChartModel _model;
    private readonly Func<IReadOnlyList<NodeModel>> _nodeSnapshot;
    private readonly ObservableCollection<ConnectionModel> _connections;
    private readonly IFlowDocumentStore _store;
    private readonly FlowDocumentMapper _mapper;
    private readonly IDialogService _dialogs;
    private readonly IUndoRedoService _undoRedo;
    private readonly Action<NodeModel> _addNode;
    private readonly Action _clearNodes;

    /// <summary>创建绑定到一个画布的文档会话。</summary>
    public FlowDocumentSession(
        FlowChartModel model,
        Func<IReadOnlyList<NodeModel>> nodeSnapshot,
        ObservableCollection<ConnectionModel> connections,
        IFlowDocumentStore store,
        FlowDocumentMapper mapper,
        IDialogService dialogs,
        IUndoRedoService undoRedo,
        Action<NodeModel> addNode,
        Action clearNodes)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _nodeSnapshot = nodeSnapshot ?? throw new ArgumentNullException(nameof(nodeSnapshot));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _undoRedo = undoRedo ?? throw new ArgumentNullException(nameof(undoRedo));
        _addNode = addNode ?? throw new ArgumentNullException(nameof(addNode));
        _clearNodes = clearNodes ?? throw new ArgumentNullException(nameof(clearNodes));
    }

    /// <summary>保存当前画布；路径为空时返回 false 且不改变状态。</summary>
    public bool Save(string? filePath)
    {
        var path = filePath ?? _model.CurrentFilePath;
        if (string.IsNullOrEmpty(path)) return false;

        try
        {
            _store.Save(path, BuildDocument());
            _model.CurrentFilePath = path;
            _model.IsModified = false;
            _model.StatusText = $"已保存：{path}";
            foreach (var node in GetNodes())
                node.OnFlowSaved();
            return true;
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"保存失败：{ex.Message}");
            _model.StatusText = "保存失败";
            return false;
        }
    }

    /// <summary>从指定文件替换当前画布；失败时发布错误并返回 false。</summary>
    public bool Load(string filePath)
    {
        try
        {
            var document = _store.Load<FlowChartDocumentModel>(filePath);
            _connections.Clear();
            _clearNodes();
            _undoRedo.Clear();
            RestoreDocument(document);
            _model.CurrentFilePath = filePath;
            _model.IsModified = false;
            _model.StatusText = $"已加载：{filePath}";
            return true;
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"加载失败：{ex.Message}");
            _model.StatusText = "加载失败";
            return false;
        }
    }

    /// <summary>将当前画布序列化为撤销/重做快照。</summary>
    public string CaptureSnapshot() =>
        JsonSerializer.Serialize(BuildDocument(), FlowDocumentJson.Options);

    /// <summary>使用快照替换当前画布；空白或无效空文档不执行任何操作。</summary>
    public void ApplySnapshot(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        var document = JsonSerializer.Deserialize<FlowChartDocumentModel>(json, FlowDocumentJson.Options);
        if (document == null) return;

        _connections.Clear();
        _clearNodes();
        RestoreDocument(document);
        _model.IsModified = true;
    }

    /// <summary>从节点文档重建节点模型。</summary>
    public NodeModel RebuildNodeFromDocument(NodeDocumentModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return _mapper.ToNode(document);
    }

    /// <summary>从节点模型构建可持久化的节点文档。</summary>
    public NodeDocumentModel BuildNodeDocument(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _mapper.ToNodeDocument(node);
    }

    /// <summary>构建当前画布的完整文档。</summary>
    private FlowChartDocumentModel BuildDocument() =>
        _mapper.ToDocument(
            _model.TabTitle,
            GetNodes(),
            _connections,
            _model);

    /// <summary>获取调用时刻的节点模型快照，避免文档模块依赖画布 ViewModel 集合。</summary>
    private IReadOnlyList<NodeModel> GetNodes() => _nodeSnapshot();

    /// <summary>从文档重建节点、连线和画布配置。</summary>
    private void RestoreDocument(FlowChartDocumentModel document)
    {
        foreach (var nodeDocument in document.Nodes)
            _addNode(_mapper.ToNode(nodeDocument));
        foreach (var connectionDocument in document.Connections)
            _connections.Add(_mapper.ToConnection(connectionDocument));
        _model.ZoomLevel = document.ZoomLevel;
        _model.TabTitle = document.Name;
    }
}
