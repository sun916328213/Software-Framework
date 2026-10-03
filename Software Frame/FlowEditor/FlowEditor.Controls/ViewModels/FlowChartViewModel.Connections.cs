// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowChartViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows.Media、FlowEditor.Controls.Models、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows.Media;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;

namespace FlowEditor.Controls.ViewModels;

/// <summary>包含临时连线提交、校验与绘制状态清理。</summary>
public partial class FlowChartViewModel
{
    /// <summary>进入从指定端口开始的连线绘制状态。</summary>
    public void StartConnection(PortModel sourcePort)
    {
        ConnectionSourcePort = sourcePort;
        IsDrawingConnection = true;
    }

    /// <summary>尝试把当前临时连线提交到目标端口。</summary>
    public bool CompleteConnection(PortModel targetPort)
    {
        if (!IsDrawingConnection || ConnectionSourcePort == null)
        {
            CancelConnection();
            return false;
        }

        var action = CreateAction(UndoRedoActionType.ConnectionAdded, "建立连线");
        var result = _connectionService.PlanConnection(
            ConnectionSourcePort,
            targetPort,
            Nodes.Select(n => n.Model).ToArray(),
            Connections.ToArray());
        if (!result.Success)
        {
            Model.StatusText = result.Error ?? "无法建立连线";
            CancelConnection();
            return false;
        }

        foreach (var connection in result.Connections)
            Connections.Add(connection);

        action.ConnectionId = result.PrimaryConnection!.Id;
        action.AfterData = _documents.CaptureSnapshot();
        UndoRedo.Push(action);

        CancelConnection();
        Model.IsModified = true;
        var status = result.CreatesCycle ? "已建立连线（注意：形成环路）" : "已建立连线";
        if (result.AutoBoundCount > 0)
            status += $"，自动绑定 {result.AutoBoundCount} 条数据连线";
        Model.StatusText = status;
        return true;
    }

    /// <summary>取消当前连线并清理临时绘制状态。</summary>
    public void CancelConnection()
    {
        IsDrawingConnection = false;
        ConnectionSourcePort = null;
        PendingConnectionGeometry = null;
    }

    /// <summary>供视图预览候选端口，复用最终提交时的同一套连接规则。</summary>
    public bool CanCompleteConnection(PortModel targetPort)
        => IsDrawingConnection &&
           ConnectionSourcePort != null &&
           _connectionService.GetRejectionReason(
               ConnectionSourcePort,
               targetPort,
               Connections,
               Nodes.Select(node => node.Model)) == null;

    /// <summary>更新视图计算出的临时连线几何图形。</summary>
    public void SetPendingGeometry(Geometry geometry)
    {
        PendingConnectionGeometry = geometry;
    }
}
