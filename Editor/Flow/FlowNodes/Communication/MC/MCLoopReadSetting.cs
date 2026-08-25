using FlowEditor.Nodes;
using Communication;
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
    /// MC 节点：将 PLC 寄存器加入后台循环读取
    /// </summary>
    [FlowTool("MC循环读取参数配置", "Memory", ToolGroup = "通讯", ToolSubGroup = "MC", SortIndex = 11)]
    public class MCLoopReadSetting : NodeModel
    {
        CommunicationManager CommunicationManager;

        public MCLoopReadSetting(IContainerProvider containerProvider)
        {
            CommunicationManager = containerProvider.Resolve<CommunicationManager>();
          
        }

        private string _registerName = "";

        [NodeProperty(DisplayName = "寄存器名", Group = "1.基础信息", Order = 10)]
        public string RegisterName
        {
            get => _registerName;
            set => SetProperty(ref _registerName, value);
        }

        private string _registerStartAdd = "";

        [NodeProperty(DisplayName = "寄存器起始地址", Group = "1.基础信息", Order = 11)]
        public string RegisterStartAdd
        {
            get => _registerStartAdd;
            set => SetProperty(ref _registerStartAdd, value);
        }

        private string _registerLength = "";

        [NodeProperty(DisplayName = "寄存器长度", Group = "1.基础信息", Order = 12)]
        public string RegisterLength
        {
            get => _registerLength;
            set => SetProperty(ref _registerLength, value);
        }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            int L = Convert.ToInt32(RegisterLength);
             CommunicationManager.SetRead(RegisterName, RegisterStartAdd, L, Extension.Core.CompareEx.EComparePolarity.None); ;
            return Task.CompletedTask;
        }
    }
}
