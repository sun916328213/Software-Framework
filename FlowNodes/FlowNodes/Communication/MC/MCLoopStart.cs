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
    [FlowTool("MC启动循环", "Memory", ToolGroup = "通讯", ToolSubGroup = "MC", SortIndex = 12)]
    internal class MCLoopStart : NodeModel
    {
        CommunicationManager CommunicationManager;

        public MCLoopStart(IContainerProvider containerProvider)
        {
            CommunicationManager = containerProvider.Resolve<CommunicationManager>();

        }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            CommunicationManager.StartLoop();
            return Task.CompletedTask;
        }
    }
}
