using FlowEditor.Nodes;
using Communication.MC;
using FlowEditor.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.MC
{
    /// <summary>
    /// MC 节点：断开 PLC 连接。
    /// 失败语义：断开指令未成功属于工具异常，抛出异常走容错路径。
    /// </summary>
    [FlowTool("MC断开连接", "Memory", ToolGroup = "通讯", ToolSubGroup = "MC", SortIndex = 2)]
    internal class MCDisConnect : NodeModel
    {
        private readonly MCHelper MCHelper;

        /// <summary>直接依赖 MC 助手（不再经由 IContainerProvider 间接解析）。</summary>
        public MCDisConnect(MCHelper mchelper)
        {
            MCHelper = mchelper ?? throw new ArgumentNullException(nameof(mchelper));
        }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            CloseResult = MCHelper.Close();
            if (!CloseResult)
                throw new InvalidOperationException("MC 断开连接失败：关闭指令未被确认");

            return Task.CompletedTask;
        }

        private bool _closeResult;
        /// <summary>断开结果（true 表示已断开）。仅当执行成功返回后才会同步到绑定输出端口。</summary>
        public bool CloseResult
        {
            get => _closeResult;
            set => SetProperty(ref _closeResult, value);
        }
    }
}
