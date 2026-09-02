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
    /// Socket 节点：轮询等待指定字节数或到达结束符的数据。
    /// 超时返回空结果不抛异常（业务NG），可通过输出端口判断。
    /// </summary>
    [FlowTool("Socket等待数据", "Memory", ToolGroup = "通讯", ToolSubGroup = "Socket", SortIndex = 40, Description = "轮询等待接收数据")]
    public class SocketWaitDataNode : NodeModel
    {
        private readonly SocketHelper SocketHelper;

        public SocketWaitDataNode(SocketHelper socketHelper)
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

        private string _endMarker = "";
        [NodeProperty(DisplayName = "结束符(可选)", Group = "1.基础信息", Order = 2, Description = "留空表示按字节数读取，填写则等待该字符串出现")]
        public string EndMarker
        {
            get => _endMarker;
            set => SetProperty(ref _endMarker, value);
        }

        private string _encodingType = "UTF-8";
        [NodeProperty(DisplayName = "编码", Group = "1.基础信息", Order = 3)]
        public string EncodingType
        {
            get => _encodingType;
            set => SetProperty(ref _encodingType, value);
        }

        private int _timeout = 5000;
        [NodeProperty(DisplayName = "超时(ms)", Group = "1.基础信息", Order = 4)]
        public int Timeout
        {
            get => _timeout;
            set => SetProperty(ref _timeout, value);
        }

        // 用于累积接收到的数据
        private readonly StringBuilder _receivedBuffer = new();
        private bool _waiting;

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!SocketHelper.IsConnected)
            {
                WaitResult = false;
                await NodeLog.ErrorAsync("Socket 等待数据失败：当前未连接");
                throw new System.InvalidOperationException("Socket 等待数据失败：当前未连接");
            }

            _receivedBuffer.Clear();
            _waiting = true;
            WaitResult = false;

            var tcs = new TaskCompletionSource<bool>();
            EventHandler<DataReceivedEventArgs> handler = (s, e) =>
            {
                if (!_waiting) return;
                _receivedBuffer.Append(Encoding.GetEncoding(EncodingType).GetString(e.Data));

                if (!string.IsNullOrEmpty(EndMarker))
                {
                    if (_receivedBuffer.ToString().Contains(EndMarker))
                        tcs.TrySetResult(true);
                }
                else if (_receivedBuffer.Length >= ExpectedLength)
                {
                    tcs.TrySetResult(true);
                }
            };

            SocketHelper.DataReceived += handler;

            try
            {
                var timeoutTask = Task.Delay(Timeout, ct);
                var completeTask = tcs.Task;
                var completed = await Task.WhenAny(completeTask, timeoutTask);

                if (completed == timeoutTask)
                {
                    var errorMsg = $"Socket 等待数据超时：期望{ExpectedLength}字节/结束符'{EndMarker}'";
                    await NodeLog.WarnAsync(errorMsg);
                    ReceivedData = _receivedBuffer.Length > 0 ? _receivedBuffer.ToString() : "";
                    WaitResult = false;
                    throw new System.TimeoutException(errorMsg);
                }

                ReceivedData = _receivedBuffer.ToString();
                WaitResult = true;
                await NodeLog.InfoAsync($"Socket 等待数据成功：收到 {_receivedBuffer.Length} 字节");
            }
            finally
            {
                _waiting = false;
                SocketHelper.DataReceived -= handler;
            }
        }

        private bool _waitResult;
        [NodeProperty(DisplayName = "等待结果", Group = "2.输出", Order = 1, IsOutput = true)]
        public bool WaitResult
        {
            get => _waitResult;
            set => SetProperty(ref _waitResult, value);
        }

        [NodeProperty(DisplayName = "接收数据", Group = "2.输出", Order = 2, IsOutput = true)]
        public string ReceivedData
        {
            get => _receivedData;
            set => SetProperty(ref _receivedData, value);
        }
        private string _receivedData = "";
    }
}
