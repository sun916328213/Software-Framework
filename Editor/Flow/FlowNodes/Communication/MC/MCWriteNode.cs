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
    /// MC 节点：向 PLC 写入寄存器数据
    /// </summary>
    [FlowTool("MC写入", "Memory", ToolGroup = "通讯", ToolSubGroup = "MC", SortIndex = 30, Description = "写入数据")]
    public class MCWriteNode : NodeModel
    {
        CommunicationManager CommunicationManager;

        public MCWriteNode(IContainerProvider containerProvider)
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

        private string _registerValue = "";
        [NodeProperty(DisplayName = "寄存器值", Group = "1.基础信息", Order = 1)]
        public string RegisterValue
        {
            get => _registerValue;
            set => SetProperty(ref _registerValue, value);
        }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            WriteResult = CommunicationManager.Write(RegisterName, RegisterValue);
            if (!WriteResult) Status = NodeStatus.Error;
            return Task.CompletedTask;
        }

        private bool _writeResult;
        /// <summary>写入结果（true 表示写入成功）</summary>
        public bool WriteResult
        {
            get => _writeResult;
            set => SetProperty(ref _writeResult, value);
        }
    }
}
