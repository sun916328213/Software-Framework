using FlowEditor.Nodes;
using Communication;
using FlowEditor.Core;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowNodes.Communication.MC
{
    [FlowTool("MC停止循环", "Memory", ToolGroup = "通讯", ToolSubGroup = "MC", SortIndex = 13)]
    internal class MCLoopStop : NodeModel
    {
        CommunicationManager CommunicationManager;

        public MCLoopStop(IContainerProvider containerProvider)
        {
            CommunicationManager = containerProvider.Resolve<CommunicationManager>();

        }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            CommunicationManager.StopLoopRead();
            return Task.CompletedTask;
        }
    }
}
