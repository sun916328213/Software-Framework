using Communication.Socket;
using FlowEditor.Core;
using FlowEditor.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.Socket
{
    /// <summary>
    /// Socket 节点：检测当前连接状态，不发起任何网络操作。
    /// 用于流程分支判断，不抛异常。
    /// </summary>
    [FlowTool("Socket连接状态", "Memory", ToolGroup = "通讯", ToolSubGroup = "Socket", SortIndex = 55, Description = "检测Socket是否已连接")]
    public class SocketIsConnectedNode : NodeModel
    {
        private readonly SocketHelper SocketHelper;

        public SocketIsConnectedNode(SocketHelper socketHelper)
        {
            SocketHelper = socketHelper ?? throw new System.ArgumentNullException(nameof(socketHelper));
        }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            IsConnected = SocketHelper.IsConnected;
            return Task.CompletedTask;
        }

        private bool _isConnected;
        [NodeProperty(DisplayName = "是否已连接", Group = "2.输出", Order = 1, IsOutput = true)]
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }
    }
}
