using FlowEditor.Core;
using FlowEditor.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EditorDemo.Node
{
    [FlowTool("计算", "Calculator", ToolGroup = "计算", SortIndex = 1, Description = "计算两个数字的和")]
    internal class Cal : NodeModel
    {

        public override Task ExecuteAsync(CancellationToken ct)
        {
            var d1 = Add(5110, 197);
            Result = d1;
            return Task.CompletedTask;
        }

        private int _result;
        /// <summary>计算结果（可绑定到输出端口）。</summary>
        public int Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }

        public int Add(int a, int b)
        {
            return a + b;
        }
    }
}
