using FlowEditor.Models;

namespace FlowEditor.ViewModels;

/// <summary>包含节点及内部连线的复制、重建和粘贴事务。</summary>
public partial class FlowChartViewModel
{
    private readonly List<NodeDocumentModel> _clipboardNodes = new();
    private readonly List<ConnectionDocumentModel> _clipboardConnections = new();

    /// <summary>复制选中的节点。</summary>
    private void CopySelected()
    {
        var selected = SelectedNodes.Count > 0
            ? SelectedNodes.ToList()
            : SelectedNode == null ? [] : [SelectedNode];
        if (selected.Count == 0) return;

        _clipboardNodes.Clear();
        _clipboardNodes.AddRange(selected.Select(n => _documents.BuildNodeDocument(n.Model)));
        var selectedIds = selected.Select(n => n.Id).ToHashSet();
        _clipboardConnections.Clear();
        _clipboardConnections.AddRange(Connections
            .Where(c => selectedIds.Contains(c.SourceNodeId) && selectedIds.Contains(c.TargetNodeId))
            .Select(c => new ConnectionDocumentModel
            {
                Id = c.Id,
                SourceNodeId = c.SourceNodeId,
                SourcePortId = c.SourcePortId,
                TargetNodeId = c.TargetNodeId,
                TargetPortId = c.TargetPortId
            }));
        Model.StatusText = $"已复制 {selected.Count} 个节点";
    }

    /// <summary>粘贴节点到画布中心。</summary>
    private void PasteAtCenter()
    {
        if (_clipboardNodes.Count == 0) return;
        var action = CreateAction(UndoRedoActionType.NodeAdded, $"粘贴 {_clipboardNodes.Count} 个节点");
        var nodeIdMap = new Dictionary<string, string>();
        var portIdMap = new Dictionary<string, string>();
        var pasted = new List<NodeViewModel>();

        foreach (var sourceDoc in _clipboardNodes)
        {
            var sourceId = sourceDoc.Id;
            var clone = CloneDocumentForPaste(sourceDoc);
            nodeIdMap[sourceId] = clone.Id;
            var newNode = _documents.RebuildNodeFromDocument(clone);
            EnsureUniqueName(newNode);
            var vm = _canvas.AddNode(newNode);
            pasted.Add(vm);

            foreach (var (key, oldPortId) in sourceDoc.PortIdMap)
            {
                var separator = key.IndexOf(':');
                if (separator < 0) continue;
                var direction = key[..separator];
                var name = key[(separator + 1)..];
                var newPort = direction == "IN"
                    ? newNode.InputPorts.FirstOrDefault(p => p.Name == name)
                    : newNode.OutputPorts.FirstOrDefault(p => p.Name == name);
                if (newPort != null) portIdMap[oldPortId] = newPort.Id;
            }
        }

        foreach (var connection in _clipboardConnections)
        {
            if (!nodeIdMap.TryGetValue(connection.SourceNodeId, out var sourceNodeId) ||
                !nodeIdMap.TryGetValue(connection.TargetNodeId, out var targetNodeId) ||
                !portIdMap.TryGetValue(connection.SourcePortId, out var sourcePortId) ||
                !portIdMap.TryGetValue(connection.TargetPortId, out var targetPortId)) continue;
            Connections.Add(new ConnectionModel
            {
                SourceNodeId = sourceNodeId,
                SourcePortId = sourcePortId,
                TargetNodeId = targetNodeId,
                TargetPortId = targetPortId
            });
        }

        action.AfterData = _documents.CaptureSnapshot();
        UndoRedo.Push(action);
        SetSelectedNodes(pasted);
        Model.IsModified = true;
        Model.StatusText = $"已粘贴 {pasted.Count} 个节点";
        RecalculateCanvasSize();
    }

    /// <summary>复制节点文档并替换节点、端口与父容器标识。</summary>
    private static NodeDocumentModel CloneDocumentForPaste(NodeDocumentModel source)
    {
        var clone = new NodeDocumentModel
        {
            Id = Guid.NewGuid().ToString("N"),
            TypeName = source.TypeName,
            Name = source.Name + "_副本",
            X = source.X + 50,
            Y = source.Y + 50,
            IsInEnabled = source.IsInEnabled,
            IsOutEnabled = source.IsOutEnabled,
            Permission = source.Permission,
            IsExcludedFromExecution = source.IsExcludedFromExecution,
            ErrorPolicy = source.ErrorPolicy,
            RetryCount = source.RetryCount,
            RetryDelayMs = source.RetryDelayMs,
            TimeoutMs = source.TimeoutMs,
            Properties = new Dictionary<string, object?>(source.Properties),
            PortIdMap = new Dictionary<string, string>(source.PortIdMap)
        };
        clone.CustomInputPorts.AddRange(source.CustomInputPorts.Select(p => new CustomPortDocument
        {
            Id = Guid.NewGuid().ToString("N"), Name = p.Name, BoundProperty = p.BoundProperty,
            DataType = p.DataType
        }));
        clone.CustomOutputPorts.AddRange(source.CustomOutputPorts.Select(p => new CustomPortDocument
        {
            Id = Guid.NewGuid().ToString("N"), Name = p.Name, BoundProperty = p.BoundProperty,
            DataType = p.DataType
        }));
        return clone;
    }
}
