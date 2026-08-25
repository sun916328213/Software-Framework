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
using SqlSugar.Extensions;

namespace FlowNodes.Communication.MC
{
    /// <summary>
    /// MC 节点：从 PLC 读取寄存器数据
    /// </summary>
    [FlowTool("MC读取", "Memory", ToolGroup = "通讯", ToolSubGroup = "MC", SortIndex = 30, Description = "读取数据")]
    public class MCReadNode : NodeModel
    {
        CommunicationManager CommunicationManager;

        public MCReadNode(IContainerProvider containerProvider)
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



        public override Task ExecuteAsync(CancellationToken ct)
        {
            ReadValue = CommunicationManager.Read(RegisterName);
            if (ReadValue is null or false)
            {
                Status = NodeStatus.Error;
                return Task.CompletedTask;
            }
            Console.WriteLine($"读取节点:{Name},读取值:{ReadValue}");
            return Task.CompletedTask;
        }

        private object? _readValue;
        /// <summary>读取到的数据（自动同步到输出端口）</summary>
        [NodeProperty(DisplayName = "读取值", Group = "1.基础信息", Order = 2, ReadOnly = 1)]
        public object? ReadValue
        {
            get => _readValue;
            set => SetProperty(ref _readValue, value);
        }
    }
}
