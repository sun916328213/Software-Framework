using FlowEditor.Nodes;
using Communication;
using Extension.Core.CompareEx;
using FlowEditor.Core;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.MC
{


    /// <summary>
    /// MC 节点：等待 PLC 寄存器值变化
    /// </summary>
    [FlowTool("MC触发", "Memory", ToolGroup = "通讯", ToolSubGroup = "MC", SortIndex = 30, Description = "MC等待")]
    public class MCWaitNode : NodeModel
    {
        CommunicationManager CommunicationManager;

        public MCWaitNode(IContainerProvider containerProvider)
        {
            CommunicationManager = containerProvider.Resolve<CommunicationManager>();
          
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
        /// <summary>
        /// 触发极性类型
        /// </summary>
        public EComparePolarity ComparePolarity
        {
            get => _comparePolarity;
            set => SetProperty(ref _comparePolarity, value);
        }


        private double curvalue,lastvalue;

        private int _pollIntervalMs = 100;
        [NodeProperty(DisplayName = "轮询间隔(ms)", Group = "1.基础信息", Order = 3)]
        public int PollIntervalMs
        {
            get => _pollIntervalMs;
            set => SetProperty(ref _pollIntervalMs, value);
        }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var read = CommunicationManager.Read(RegisterName);
                if (read is null or false)
                {
                    Status = NodeStatus.Error;
                    return;
                }
                curvalue = Convert.ToDouble(read);

                var triggered = ComparePolarity switch
                {
                    EComparePolarity.RisingEdge => curvalue > lastvalue,
                    EComparePolarity.FallingEdge => curvalue < lastvalue,
                    EComparePolarity.BothEdges => curvalue > lastvalue || curvalue < lastvalue,
                    _ => false
                };
                if (triggered) return;

                lastvalue = curvalue;
                await Task.Delay(Math.Max(1, PollIntervalMs), ct);
            }
        }
    }
}
