// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CommunicationNodeDependencyTests。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Runtime.Composition、Communication.Runtime.Core、FlowEditor.Controls.Core、FlowEditor.Controls.Nodes、FlowNodes.Integration。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Runtime.Composition;
using Communication.Runtime.Core;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Nodes;
using FlowNodes.Integration;
using Xunit;

namespace FlowNodes.Workbench.Tests;

/// <summary>
/// T14：通讯节点的构造依赖必须停在 Core 契约 <see cref="ICommunicationManager"/> 上，
/// 不能绑到 Runtime 实现 <see cref="CommunicationManager"/>。
/// 宿主把两者注册成同一个单例，所以「解析得到同一个实例」的测试对这次收窄是无感的；
/// 本文件改用只提供 Core 契约的解析器，任一节点若退回具体类就解析失败。
/// </summary>
public sealed class CommunicationNodeDependencyTests
{
    /// <summary>通讯节点的类型名清单（namespace = FlowNodes.Communication 的全部具体节点）。</summary>
    private static readonly string[] ExpectedNodeNames =
    [
        "NetDisconnectNode",
        "NetHttpConnectNode",
        "NetMcConnectNode",
        "NetModbusRtuConnectNode",
        "NetModbusTcpConnectNode",
        "NetOpcUaConnectNode",
        "NetReadNode",
        "NetReceiveNode",
        "NetRequestNode",
        "NetSendNode",
        "NetSerialConnectNode",
        "NetStatusNode",
        "NetTcpConnectNode",
        "NetUdpConnectNode",
        "NetWaitNode",
        "NetWebApiConnectNode",
        "NetWriteNode",
    ];

    /// <summary>
    /// 只把 <see cref="ICommunicationManager"/> 放进解析器，故意不提供 <see cref="CommunicationManager"/>：
    /// 每个通讯节点都必须能用 Core 契约构造出来。
    /// </summary>
    [Fact]
    public void 通讯节点只需Core管理器契约即可构造()
    {
        var nodeTypes = CommunicationNodeTypes();

        // 先钉住枚举本身：类型集合变了说明有新节点漏进这个测试，先修测试再谈结论。
        Assert.Equal(ExpectedNodeNames, nodeTypes.Select(type => type.Name).ToArray());

        using var manager = new CommunicationManager(CommunicationDefaults.CreateDefaultFactory());
        var factory = new ReflectionNodeFactory(
            type => type == typeof(ICommunicationManager) ? manager : null);

        var failures = new List<string>();
        foreach (var type in nodeTypes)
        {
            try
            {
                _ = factory.Create(type);
            }
            catch (Exception error)
            {
                failures.Add($"{type.Name}: {error.Message}");
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>解析器换成具体类时构造必须失败——反向对照，证明上一条用例真的在考依赖类型。</summary>
    [Fact]
    public void 解析器只提供具体管理器类型时Core契约缺失必须暴露()
    {
        using var manager = new CommunicationManager(CommunicationDefaults.CreateDefaultFactory());
        var factory = new ReflectionNodeFactory(
            type => type == typeof(CommunicationManager) ? manager : null);

        var type = CommunicationNodeTypes().Single(candidate => candidate.Name == "NetReadNode");
        var error = Assert.Throws<InvalidOperationException>(() => factory.Create(type));

        Assert.Contains("ICommunicationManager", error.Message, StringComparison.Ordinal);
    }

    private static Type[] CommunicationNodeTypes() =>
        typeof(FlowNodesComposition).Assembly.GetTypes()
            .Where(type => type.Namespace == "FlowNodes.Communication")
            .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(NodeModel)))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
}
