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
    /// Socket 节点：发送原始字节数组并等待响应。
    /// 失败语义：未连接、发送失败或超时均属于工具异常，抛出异常走容错路径。
    /// </summary>
    [FlowTool("Socket发送并接收_字节", "Memory", ToolGroup = "通讯", ToolSubGroup = "Socket", SortIndex = 65, Description = "发送字节数组并等待响应")]
    public class SocketSendBytesNode : NodeModel
    {
        private readonly SocketHelper SocketHelper;

        public SocketSendBytesNode(SocketHelper socketHelper)
        {
            SocketHelper = socketHelper ?? throw new System.ArgumentNullException(nameof(socketHelper));
        }

        [NodeProperty(DisplayName = "发送字节(Hex字符串)", Group = "1.基础信息", Order = 1, IsInput = true, Description = "十六进制格式，如：48 65 6C 6C 6F 或 48656C6C6F")]
        public string SendHexData
        {
            get => _sendHexData;
            set => SetProperty(ref _sendHexData, value);
        }
        private string _sendHexData = "";

        private int _timeout = 5000;
        [NodeProperty(DisplayName = "超时(ms)", Group = "1.基础信息", Order = 2)]
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
                SendResult = false;
                await NodeLog.ErrorAsync("Socket 发送字节失败：当前未连接");
                throw new System.InvalidOperationException("Socket 发送字节失败：当前未连接");
            }

            byte[] sendData = HexToBytes(SendHexData);
            byte[] response = await SocketHelper.SendAndReceive(sendData, Timeout);

            if (response == null)
            {
                SendResult = false;
                var errorMsg = $"Socket 接收超时：发送={SendHexData}";
                await NodeLog.ErrorAsync(errorMsg);
                throw new System.TimeoutException(errorMsg);
            }

            ResponseHexData = BytesToHex(response);
            SendResult = true;
            await NodeLog.InfoAsync($"Socket 发送并接收字节成功：发送={SendHexData}，接收={ResponseHexData}");
        }

        private bool _sendResult;
        [NodeProperty(DisplayName = "发送结果", Group = "2.输出", Order = 1, IsOutput = true)]
        public bool SendResult
        {
            get => _sendResult;
            set => SetProperty(ref _sendResult, value);
        }

        [NodeProperty(DisplayName = "响应字节(Hex)", Group = "2.输出", Order = 2, IsOutput = true)]
        public string ResponseHexData
        {
            get => _responseHexData;
            set => SetProperty(ref _responseHexData, value);
        }
        private string _responseHexData = "";

        private static byte[] HexToBytes(string hex)
        {
            hex = hex.Replace(" ", "").Replace(":", "");
            if (hex.Length % 2 != 0)
                hex = "0" + hex;
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        private static string BytesToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", " ");
        }
    }
}
