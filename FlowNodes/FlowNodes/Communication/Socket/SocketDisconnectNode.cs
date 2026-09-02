using Communication.Socket;
using FlowEditor.Core;
using FlowEditor.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.Socket
{
    /// <summary>
    /// Socket 节点：断开与服务器的连接。
    /// 失败语义：断开失败属于工具异常，抛出异常走容错路径。
    /// </summary>
    [FlowTool("Socket断开连接", "Memory", ToolGroup = "通讯", ToolSubGroup = "Socket", SortIndex = 11, Description = "断开Socket连接")]
    public class SocketDisconnectNode : NodeModel
    {
        private readonly SocketHelper SocketHelper;

        public SocketDisconnectNode(SocketHelper socketHelper)
        {
            SocketHelper = socketHelper ?? throw new System.ArgumentNullException(nameof(socketHelper));
        }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            CloseResult = !SocketHelper.IsConnected;
            if (!CloseResult)
            {
                SocketHelper.Disconnect();
                CloseResult = !SocketHelper.IsConnected;
            }

            if (!CloseResult)
            {
                var errorMsg = "Socket 断开连接失败：连接状态异常";
                _ = NodeLog.ErrorAsync(errorMsg);
                throw new System.InvalidOperationException(errorMsg);
            }

            return Task.CompletedTask;
        }

        private bool _closeResult;
        [NodeProperty(DisplayName = "断开结果", Group = "2.输出", Order = 1, IsOutput = true)]
        public bool CloseResult
        {
            get => _closeResult;
            set => SetProperty(ref _closeResult, value);
        }
    }
}
