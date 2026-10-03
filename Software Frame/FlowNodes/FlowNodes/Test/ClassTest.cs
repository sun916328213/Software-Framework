// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：Testclass、ClassTest。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes。导入不代表所有类型都会在运行时加载。
using FlowNodes;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowNodes.Test
{
    /// <summary>
    /// Testclass 的类型定义。FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
    /// </summary>
    public class Testclass
    {
        public string ID { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// ClassTest 的类型定义。FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
    /// 继承或实现的契约：NodeModel。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

        /// <summary>
        /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct)
        {
            Testclass= new Testclass() { ID = "11", Name = "Test1" };
            return Task.CompletedTask;
        }
    }
}
