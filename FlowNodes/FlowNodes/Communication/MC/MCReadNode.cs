using FlowEditor.Nodes;
using Communication;
using FlowEditor.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.MC
{
    /// <summary>
    /// MC 节点：从 PLC 读取寄存器数据。
    /// 失败语义：读取失败（null/false）意味着没有取得有效数据，副作用语义下
    /// 下游不应消费旧值——抛出异常走容错路径（可配置 Retry 应对瞬时抖动）。
    /// </summary>
    [FlowTool("MC读取", "Memory", ToolGroup = "通讯", ToolSubGroup = "MC", SortIndex = 30, Description = "读取数据")]
    public class MCReadNode : NodeModel
    {
        private readonly CommunicationManager CommunicationManager;

        /// <summary>直接依赖通讯管理器（不再经由 IContainerProvider 间接解析）。</summary>
        public MCReadNode(CommunicationManager communicationManager)
        {
            CommunicationManager = communicationManager ?? throw new ArgumentNullException(nameof(communicationManager));
        }

        private string _registerName = "";

        [NodeProperty(DisplayName = "寄存器名称", Group = "1.基础信息", Order = 1)]
        public string RegisterName
        {
            get => _registerName;
            set => SetProperty(ref _registerName, value);
        }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            ReadValue = CommunicationManager.Read(RegisterName);
            if (ReadValue is null or false)
            {
                string errorMessage = $"MC 读取失败：寄存器 [{RegisterName}] 未返回有效数据";
                await NodeLog.ErrorAsync(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
            await NodeLog.InfoAsync($"读取节点:{Name},读取值:{ReadValue}");
        }

        private object? _readValue;
        /// <summary>读取到的数据（自动同步到输出端口）。仅当执行成功返回后才会同步。</summary>
        [NodeProperty(DisplayName = "读取值", Group = "1.基础信息", Order = 2, ReadOnly = 1)]
        public object? ReadValue
        {
            get => _readValue;
            set => SetProperty(ref _readValue, value);
        }
    }
}
