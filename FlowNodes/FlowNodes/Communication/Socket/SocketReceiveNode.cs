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
    /// Socket 节点：从服务器接收数据（单次读取，阻塞等待直到收到数据或超时）。
    /// 超时属于工具异常，抛出异常走容错路径。
    /// </summary>
    [FlowTool("Socket接收数据", "Memory", ToolGroup = "通讯", ToolSubGroup = "Socket", SortIndex = 45, Description = "接收服务器数据")]
    public class SocketReceiveNode : NodeModel
    {
        private readonly SocketHelper SocketHelper;

        public SocketReceiveNode(SocketHelper socketHelper)
        {
            SocketHelper = socketHelper ?? throw new System.ArgumentNullException(nameof(socketHelper));
        }

        private int _expectedLength = 1024;
        [NodeProperty(DisplayName = "期望字节数", Group = "1.基础信息", Order = 1)]
        public int ExpectedLength
        {
            get => _expectedLength;
            set => SetProperty(ref _expectedLength, value);
        }

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

        private readonly StringBuilder _buffer = new();
        private bool _receiving;

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!SocketHelper.IsConnected)
            {
                ReceiveResult = false;
                await NodeLog.ErrorAsync("Socket 接收失败：当前未连接");
                throw new System.InvalidOperationException("Socket 接收失败：当前未连接");
            }

            _buffer.Clear();
            _receiving = true;
            ReceiveResult = false;

            var tcs = new TaskCompletionSource<bool>();
            EventHandler<DataReceivedEventArgs> handler = (s, e) =>
            {
                if (!_receiving) return;
                _buffer.Append(Encoding.GetEncoding(EncodingType).GetString(e.Data));
                if (_buffer.Length >= ExpectedLength)
                    tcs.TrySetResult(true);
            };

            SocketHelper.DataReceived += handler;

            try
            {
                var timeoutTask = Task.Delay(Timeout, ct);
                var completed = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completed == timeoutTask)
                {
                    await NodeLog.WarnAsync($"Socket 接收超时：期望{ExpectedLength}字节");
                    ReceivedString = _buffer.Length > 0 ? _buffer.ToString() : "";
                    ReceiveResult = false;
                    throw new System.TimeoutException($"Socket 接收超时：期望{ExpectedLength}字节");
                }

                ReceivedString = _buffer.ToString();
                ReceivedBytes = Encoding.GetEncoding(EncodingType).GetBytes(ReceivedString);
                ReceiveResult = true;
                await NodeLog.InfoAsync($"Socket 接收成功：{_buffer.Length} 字节");
            }
            finally
            {
                _receiving = false;
                SocketHelper.DataReceived -= handler;
            }
        }

        private bool _receiveResult;
        [NodeProperty(DisplayName = "接收结果", Group = "2.输出", Order = 1, IsOutput = true)]
        public bool ReceiveResult
        {
            get => _receiveResult;
            set => SetProperty(ref _receiveResult, value);
        }

        [NodeProperty(DisplayName = "接收字符串", Group = "2.输出", Order = 2, IsOutput = true)]
        public string ReceivedString
        {
            get => _receivedString;
            set => SetProperty(ref _receivedString, value);
        }
        private string _receivedString = "";

        [NodeProperty(DisplayName = "接收字节", Group = "2.输出", Order = 3, IsOutput = true)]
        public byte[]? ReceivedBytes
        {
            get => _receivedBytes;
            set => SetProperty(ref _receivedBytes, value);
        }
        private byte[]? _receivedBytes;
    }
}
