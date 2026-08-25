using FlowEditor.Nodes;
using Communication;
using Communication.MC;
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
    [FlowTool("MC断开连接", "Memory", ToolGroup = "通讯", ToolSubGroup = "MC", SortIndex = 2)]
    internal class MCDisConnect : NodeModel
    {
        MCHelper  MCHelper;

        public MCDisConnect(IContainerProvider containerProvider)
        {
            MCHelper = containerProvider.Resolve<MCHelper>();

        }
 
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            var ret = MCHelper.Close();
            CloseResult = ret;
            if (!ret) Status = NodeStatus.Error;
            await Task.CompletedTask;
        }

        private bool _closeResult;
        /// <summary>断开结果（true 表示已断开）</summary>
        public bool CloseResult
        {
            get => _closeResult;
            set => SetProperty(ref _closeResult, value);
        }
    }
}
