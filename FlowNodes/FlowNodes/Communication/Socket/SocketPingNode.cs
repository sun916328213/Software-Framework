using Communication.Socket;
using FlowEditor.Core;
using FlowEditor.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.Socket
{
    /// <summary>
    /// Socket 节点：心跳检测——发送 "ping" 并等待 "pong" 响应，验证连接是否活跃。
    /// 超时或无响应均属于工具异常，抛出异常走容错路径。
    /// </summary>
    [FlowTool("Socket心跳检测", "Memory", ToolGroup = "通讯", ToolSubGroup = "Socket", SortIndex = 50, Description = "发送ping检测连接状态")]
    public class SocketPingNode : NodeModel
    {
        private readonly SocketHelper SocketHelper;

        public SocketPingNode(SocketHelper socketHelper)
        {
            SocketHelper = socketHelper ?? throw new System.ArgumentNullException(nameof(socketHelper));
        }

        private int _timeout = 3000;
        [NodeProperty(DisplayName = "超时(ms)", Group = "1.基础信息", Order = 1)]
        public int Timeout
        {
            get => _timeout;
            set => SetProperty(ref _timeout, value);
        }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!SocketHelper.IsConnected)
            {
                PingResult = false;
                await NodeLog.ErrorAsync("Socket 心跳失败：当前未连接");
                throw new System.InvalidOperationException("Socket 心跳失败：当前未连接");
            }

            byte[] pingBytes = System.Text.Encoding.UTF8.GetBytes("ping");
            byte[] response = await SocketHelper.SendAndReceive(pingBytes, Timeout);

            if (response == null)
            {
                PingResult = false;
                var errorMsg = "Socket 心跳超时：未收到 pong 响应";
                await NodeLog.ErrorAsync(errorMsg);
                throw new System.TimeoutException(errorMsg);
            }

            PingResult = true;
            PingResponse = System.Text.Encoding.UTF8.GetString(response);
            await NodeLog.InfoAsync($"Socket 心跳成功：响应={PingResponse}");
        }

        private bool _pingResult;
        [NodeProperty(DisplayName = "心跳结果", Group = "2.输出", Order = 1, IsOutput = true)]
        public bool PingResult
        {
            get => _pingResult;
            set => SetProperty(ref _pingResult, value);
        }

        [NodeProperty(DisplayName = "响应内容", Group = "2.输出", Order = 2, IsOutput = true)]
        public string PingResponse
        {
            get => _pingResponse;
            set => SetProperty(ref _pingResponse, value);
        }
        private string _pingResponse = "";
    }
}
