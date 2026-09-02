using Communication.Socket;
using FlowEditor.Core;
using FlowEditor.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.Socket
{
    /// <summary>
    /// Socket 节点：连接指定 IP 和端口的服务器。
    /// 失败语义：连接未建立属于工具异常，抛出异常走容错路径（默认 FailFast；可配置 Retry）。
    /// </summary>
    [FlowTool("Socket连接", "Memory", ToolGroup = "通讯", ToolSubGroup = "Socket", SortIndex = 10, Description = "连接到指定IP和端口")]
    public class SocketConnectNode : NodeModel
    {
        private readonly SocketHelper SocketHelper;

        public SocketConnectNode(SocketHelper socketHelper)
        {
            SocketHelper = socketHelper ?? throw new System.ArgumentNullException(nameof(socketHelper));
        }

        private string _serverIP = "127.0.0.1";
        [NodeProperty(DisplayName = "服务器IP", Group = "1.基础信息", Order = 1)]
        public string ServerIP
        {
            get => _serverIP;
            set => SetProperty(ref _serverIP, value);
        }

        private int _port = 8080;
        [NodeProperty(DisplayName = "端口", Group = "1.基础信息", Order = 2)]
        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        private int _timeout = 5000;
        [NodeProperty(DisplayName = "超时(ms)", Group = "1.基础信息", Order = 3)]
        public int Timeout
        {
            get => _timeout;
            set => SetProperty(ref _timeout, value);
        }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            SocketHelper.SocketParam.ServerIP = ServerIP;
            SocketHelper.SocketParam.Port = Port;
            SocketHelper.SocketParam.ConnectTimeout = Timeout;
            SocketHelper.SocketParam.ReceiveTimeout = Timeout;
            SocketHelper.SocketParam.SendTimeout = Timeout;

            ConnectResult = SocketHelper.Connect();

            if (!ConnectResult)
            {
                var errorMsg = $"Socket 连接失败：[{ServerIP}:{Port}] 未能建立连接";
                await NodeLog.ErrorAsync(errorMsg);
                throw new System.InvalidOperationException(errorMsg);
            }

            await NodeLog.InfoAsync($"Socket 连接成功：[{ServerIP}:{Port}]");
        }

        private bool _connectResult;
        [NodeProperty(DisplayName = "连接结果", Group = "2.输出", Order = 1, IsOutput = true)]
        public bool ConnectResult
        {
            get => _connectResult;
            set => SetProperty(ref _connectResult, value);
        }
    }
}
