using Communication.Socket;
using FlowEditor.Core;
using FlowEditor.Nodes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.Socket
{
    /// <summary>
    /// Socket 节点：发送数据并等待服务器响应。
    /// 失败语义：未连接、发送失败或超时均属于工具异常，抛出异常走容错路径。
    /// </summary>
    [FlowTool("Socket发送并接收", "Memory", ToolGroup = "通讯", ToolSubGroup = "Socket", SortIndex = 30, Description = "发送并等待响应")]
    public class SocketSendAndReceiveNode : NodeModel
    {
        private readonly SocketHelper SocketHelper;

        public SocketSendAndReceiveNode(SocketHelper socketHelper)
        {
            SocketHelper = socketHelper ?? throw new System.ArgumentNullException(nameof(socketHelper));
        }

        [NodeProperty(DisplayName = "发送内容", Group = "1.基础信息", Order = 1, IsInput = true)]
        public string SendData
        {
            get => _sendData;
            set => SetProperty(ref _sendData, value);
        }
        private string _sendData = "";

        private string _encodingType = "UTF-8";
        [NodeProperty(DisplayName = "编码", Group = "1.基础信息", Order = 2)]
        public string EncodingType
        {
            get => _encodingType;
            set => SetProperty(ref _encodingType, value);
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

            if (!SocketHelper.IsConnected)
            {
                var errorMsg = "Socket 发送并接收失败：当前未连接";
                await NodeLog.ErrorAsync(errorMsg);
                throw new System.InvalidOperationException(errorMsg);
            }

            byte[] sendBytes = Encoding.GetEncoding(EncodingType).GetBytes(SendData);
            byte[] responseBytes = await SocketHelper.SendAndReceive(sendBytes, Timeout);

            if (responseBytes == null)
            {
                var errorMsg = $"Socket 接收超时或无响应：发送={SendData}";
                await NodeLog.ErrorAsync(errorMsg);
                throw new System.TimeoutException(errorMsg);
            }

            ResponseData = Encoding.GetEncoding(EncodingType).GetString(responseBytes);
            ReceiveResult = true;

            await NodeLog.InfoAsync($"Socket 发送并接收成功：发送={SendData}，接收={ResponseData}");
        }

        private bool _receiveResult;
        [NodeProperty(DisplayName = "接收结果", Group = "2.输出", Order = 1, IsOutput = true)]
        public bool ReceiveResult
        {
            get => _receiveResult;
            set => SetProperty(ref _receiveResult, value);
        }

        [NodeProperty(DisplayName = "响应数据", Group = "2.输出", Order = 2, IsOutput = true)]
        public string ResponseData
        {
            get => _responseData;
            set => SetProperty(ref _responseData, value);
        }
        private string _responseData = "";
    }
}
