using Communication;
using FlowEditor.Core;
using FlowEditor.Nodes;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Threading;
using System.Threading.Tasks;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.MC
{
    /// <summary>
    /// MC 节点：向 PLC 写入寄存器数据。
    /// 失败语义：写入未落盘（WriteResult=false）属于工具异常而非业务 NG——
    /// 副作用未发生，下游基于的是虚假系统状态，因此抛出异常走容错路径：
    /// 默认 FailFast 终止；可按节点配置 Retry（PLC 忙重写）或 RouteToErrorPort（进报警支线）。
    /// 业务上需要"写失败也继续"的场景，请为该节点配置 ContinueOnError 策略，
    /// 或改用 ConditionNode 判断下游反馈建模。
    /// </summary>
    [FlowTool("MC写入", "Memory", ToolGroup = "通讯", ToolSubGroup = "MC", SortIndex = 30, Description = "写入数据")]
    public class MCWriteNode : NodeModel
    {
        private readonly CommunicationManager CommunicationManager;

        /// <summary>直接依赖通讯管理器（不再经由 IContainerProvider 间接解析）。</summary>
        public MCWriteNode(CommunicationManager communicationManager)
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

        private string _registerValue = "";
        [NodeProperty(DisplayName = "寄存器值", Group = "1.基础信息", Order = 1)]
        public string RegisterValue
        {
            get => _registerValue;
            set => SetProperty(ref _registerValue, value);
        }

        /// <summary>
        /// 执行写入。失败时抛出 InvalidOperationException（引擎据此触发容错策略）；
        /// WriteResult 属性保留 false 供属性面板检查（注意：异常路径下输出端口不会同步）。
        /// </summary>
        public override async Task<Task> ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            WriteResult = CommunicationManager.Write(RegisterName, RegisterValue);
            if (!WriteResult)
            {
                await NodeLog.Error($"MC 触发等待：寄存器 [{RegisterName}] 写入 [{RegisterValue}] 未成功");
                throw new InvalidOperationException(
                    $"MC 写入失败：寄存器 [{RegisterName}] 写入 [{RegisterValue}] 未成功（副作用未发生，流程不应继续）");

            }
            await NodeLog.Info($"MC 触发等待：寄存器 [{RegisterName}] 写入 [{RegisterValue}] 成功");
            return Task.CompletedTask;
        }

        private bool _writeResult;
        /// <summary>写入结果（true 表示写入成功）。仅当执行成功返回后才会同步到绑定输出端口。</summary>
        public bool WriteResult
        {
            get => _writeResult;
            set => SetProperty(ref _writeResult, value);
        }
    }
}
