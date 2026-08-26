using FlowEditor.Nodes;
using Communication;
using Communication.MC;
using FlowEditor.Core;
using System.Threading.Tasks;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.MC
{
    /// <summary>
    /// MC 节点：连接 PLC 站点。
    /// 失败语义：连接未建立属于工具异常，抛出异常走容错路径
    /// （默认 FailFast；可配置 Retry 应对瞬时占用）。
    /// </summary>
    [FlowTool("MC连接", "Memory", ToolGroup = "通讯", ToolSubGroup = "MC", SortIndex = 1)]
    internal class MCConnect : NodeModel
    {
        private readonly MCHelper MCHelper;

        /// <summary>直接依赖 MC 助手（不再经由 IContainerProvider 间接解析）。</summary>
        public MCConnect(MCHelper mchelper)
        {
            MCHelper = mchelper ?? throw new System.ArgumentNullException(nameof(mchelper));
        }

        private int _deviceID;

        [NodeProperty(DisplayName = "站点ID", Group = "1.基础信息", Order = 10)]
        public int DeviceID
        {
            get => _deviceID;
            set => SetProperty(ref _deviceID, value);
        }

        public override Task ExecuteAsync(System.Threading.CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            ConnectResult = MCHelper.Connect(DeviceID);
            if (!ConnectResult)
                throw new System.InvalidOperationException(
                    $"MC 连接失败：站点 [{DeviceID}] 未能建立连接");

            return Task.CompletedTask;
        }

        private bool _connectResult;
        /// <summary>连接结果。仅当执行成功返回后才会同步到绑定输出端口。</summary>
        public bool ConnectResult
        {
            get => _connectResult;
            set => SetProperty(ref _connectResult, value);
        }
    }
}
