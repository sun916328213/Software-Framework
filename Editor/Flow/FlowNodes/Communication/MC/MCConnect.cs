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
    [FlowTool("MC连接", "Memory", ToolGroup = "通讯", ToolSubGroup = "MC", SortIndex = 1)]
    internal class MCConnect: NodeModel
    {
        MCHelper  MCHelper;

        public MCConnect(IContainerProvider containerProvider)
        {
            MCHelper = containerProvider.Resolve<MCHelper>();

        }

        private int _deviceID ;

        [NodeProperty(DisplayName = "??ID", Group = "1.????", Order = 10)]
        public int DeviceID
        {
            get => _deviceID;
            set => SetProperty(ref _deviceID, value);
        }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            var ret = MCHelper.Connect(DeviceID);
            ConnectResult = ret;
            if (!ret) Status = NodeStatus.Error;
            await Task.CompletedTask;
        }

        private bool _connectResult;
        /// <summary>连接结果</summary>
        public bool ConnectResult
        {
            get => _connectResult;
            set => SetProperty(ref _connectResult, value);
        }
    }
}
