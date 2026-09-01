using FlowEditor.Nodes;
using Communication;
using Extension.Core.CompareEx;
using FlowEditor.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using WPFCustomControls.PropertyGrid.Models;
using Prism.Ioc;

namespace FlowNodes.Communication.MC
{
    /// <summary>
    /// MC 节点：等待 PLC 寄存器值变化（边沿触发）。
    /// 失败语义：单次读取失败视为通讯抖动，按「读取重试次数」连续重试；
    /// 超过阈值仍失败才判定为工具异常并抛出（默认 FailFast 终止流程）。
    /// lastvalue 跨轮保留：循环运行模式下边沿检测在轮次间保持连续。
    /// </summary>
    [FlowTool("MC触发", "Memory", ToolGroup = "通讯", ToolSubGroup = "MC", SortIndex = 30, Description = "MC等待")]
    public class MCWaitNode : NodeModel
    {
          readonly CommunicationManager CommunicationManager;

        /// <summary>直接依赖通讯管理器（不再经由 IContainerProvider 间接解析）。</summary>
        public MCWaitNode(IContainerProvider  container)
        {
            CommunicationManager = container.Resolve<CommunicationManager>();
        }

        private string _registerName = "";

        [NodeProperty(DisplayName = "寄存器名称", Group = "1.基础信息", Order = 1)]
        public string RegisterName
        {
            get => _registerName;
            set => SetProperty(ref _registerName, value);
        }

        private EComparePolarity _comparePolarity = EComparePolarity.RisingEdge;
        [NodeProperty(DisplayName = "触发极性", Group = "1.基础信息", Order = 2)]
        /// <summary>触发极性类型</summary>
        public EComparePolarity ComparePolarity
        {
            get => _comparePolarity;
            set => SetProperty(ref _comparePolarity, value);
        }

        private double curvalue, lastvalue;

        private int _pollIntervalMs = 100;
        [NodeProperty(DisplayName = "轮询间隔(ms)", Group = "1.基础信息", Order = 3)]
        public int PollIntervalMs
        {
            get => _pollIntervalMs;
            set => SetProperty(ref _pollIntervalMs, value);
        }

        private int _readRetryCount = 3;
        [NodeProperty(DisplayName = "读取重试次数", Group = "1.基础信息", Order = 4)]
        /// <summary>连续读取失败的重试上限（0 = 首次失败即判死），超过后抛出异常。</summary>
        public int ReadRetryCount
        {
            get => _readRetryCount;
            set => SetProperty(ref _readRetryCount, Math.Clamp(value, 0, 10));
        }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            var readFailStreak = 0;

            while (!ct.IsCancellationRequested)
            {
                var read = CommunicationManager.Read(RegisterName);

                // 读抖动容错：连续失败超过阈值才判死（等待场景最忌因瞬时抖动提前退出）
                if (read is null or false)
                {
                    readFailStreak++;
                    if (readFailStreak > Math.Max(0, ReadRetryCount))
                        throw new InvalidOperationException(
                            $"MC 触发等待失败：寄存器 [{RegisterName}] 连续 {readFailStreak} 次读取无效");
                    await Task.Delay(Math.Max(1, PollIntervalMs), ct);
                    continue;
                }
                readFailStreak = 0;

                curvalue = Convert.ToDouble(read);

                var triggered = ComparePolarity switch
                {
                    EComparePolarity.RisingEdge => curvalue > lastvalue,
                    EComparePolarity.FallingEdge => curvalue < lastvalue,
                    EComparePolarity.BothEdges => curvalue > lastvalue || curvalue < lastvalue,
                    _ => false
                };
                if (triggered)
                {
                    await NodeLog.Info($"MC 触发等待：寄存器 [{RegisterName}] 当前值 {curvalue}，上次值 {lastvalue}，触发");
                    return;
                }
 
                lastvalue = curvalue;
                await Task.Delay(Math.Max(1, PollIntervalMs), ct);
            }
        }
    }
}
