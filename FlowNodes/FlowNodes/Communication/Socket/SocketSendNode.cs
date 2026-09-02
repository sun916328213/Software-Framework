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
    /// Socket 节点：发送字符串数据到服务器。
    /// 失败语义：未连接或发送失败属于工具异常，抛出异常走容错路径。
    /// </summary>
    [FlowTool("Socket发送数据", "Memory", ToolGroup = "通讯", ToolSubGroup = "Socket", SortIndex = 20, Description = "发送字符串数据")]
    public class SocketSendNode : NodeModel
    {
        private readonly SocketHelper SocketHelper;

        public SocketSendNode(SocketHelper socketHelper)
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
                var errorMsg = "Socket 发送失败：当前未连接";
                await NodeLog.ErrorAsync(errorMsg);
                throw new System.InvalidOperationException(errorMsg);
            }

            byte[] data = Encoding.GetEncoding(EncodingType).GetBytes(SendData);
            SendResult = SocketHelper.Send(data);

            if (!SendResult)
            {
                var errorMsg = $"Socket 发送失败：{SendData}";
                await NodeLog.ErrorAsync(errorMsg);
                throw new System.InvalidOperationException(errorMsg);
            }

            await NodeLog.InfoAsync($"Socket 发送成功：{SendData}");
        }

        private bool _sendResult;
        [NodeProperty(DisplayName = "发送结果", Group = "2.输出", Order = 1, IsOutput = true)]
        public bool SendResult
        {
            get => _sendResult;
            set => SetProperty(ref _sendResult, value);
        }
    }
}
