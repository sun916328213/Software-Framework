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
    /// Socket 节点：批量发送多条数据，全部发送成功才返回。
    /// 任意一条发送失败均属于工具异常，抛出异常走容错路径。
    /// </summary>
    [FlowTool("Socket批量发送", "Memory", ToolGroup = "通讯", ToolSubGroup = "Socket", SortIndex = 60, Description = "批量发送多条数据")]
    public class SocketBatchSendNode : NodeModel
    {
        private readonly SocketHelper SocketHelper;

        public SocketBatchSendNode(SocketHelper socketHelper)
        {
            SocketHelper = socketHelper ?? throw new System.ArgumentNullException(nameof(socketHelper));
        }

        [NodeProperty(DisplayName = "发送内容(逗号分隔)", Group = "1.基础信息", Order = 1, IsInput = true, Description = "多条数据用逗号分隔，如：data1,data2,data3")]
        public string BatchData
        {
            get => _batchData;
            set => SetProperty(ref _batchData, value);
        }
        private string _batchData = "";

        private string _encodingType = "UTF-8";
        [NodeProperty(DisplayName = "编码", Group = "1.基础信息", Order = 2)]
        public string EncodingType
        {
            get => _encodingType;
            set => SetProperty(ref _encodingType, value);
        }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!SocketHelper.IsConnected)
            {
                BatchSendResult = false;
                await NodeLog.ErrorAsync("Socket 批量发送失败：当前未连接");
                throw new System.InvalidOperationException("Socket 批量发送失败：当前未连接");
            }

            var items = BatchData.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (items.Length == 0)
            {
                await NodeLog.WarnAsync("Socket 批量发送：输入为空");
                BatchSendResult = true;
                return;
            }

            var byteList = new System.Collections.Generic.List<byte[]>();
            foreach (var item in items)
                byteList.Add(Encoding.GetEncoding(EncodingType).GetBytes(item));

            BatchSendResult = SocketHelper.SendBatch(byteList);

            if (!BatchSendResult)
            {
                var errorMsg = $"Socket 批量发送失败：共 {items.Length} 条数据";
                await NodeLog.ErrorAsync(errorMsg);
                throw new System.InvalidOperationException(errorMsg);
            }

            await NodeLog.InfoAsync($"Socket 批量发送成功：{items.Length} 条");
        }

        private bool _batchSendResult;
        [NodeProperty(DisplayName = "发送结果", Group = "2.输出", Order = 1, IsOutput = true)]
        public bool BatchSendResult
        {
            get => _batchSendResult;
            set => SetProperty(ref _batchSendResult, value);
        }
    }
}
