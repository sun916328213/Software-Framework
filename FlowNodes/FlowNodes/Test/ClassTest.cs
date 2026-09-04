using FlowEditor.Core;
using FlowEditor.Nodes;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Test
{
    public class Testclass
    {
        public string ID { get; set; }
        public string Name { get; set; }
    }

    [FlowTool("ClassTest", "Test", ToolGroup = "Test", SortIndex = 20, Description = "执行 C# 脚本代码")]

    internal class ClassTest : NodeModel
    {

        Testclass testclass = new Testclass();
        [NodeProperty(DisplayName = "testclass", Group = "1.基本参数", Order = 1, Description = "testclass")]
        public Testclass Testclass
        {
            get => testclass;
            set => SetProperty(ref testclass, value);
        }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            Testclass= new Testclass() { ID = "11", Name = "Test1" };
            return Task.CompletedTask;
        }
    }
}
