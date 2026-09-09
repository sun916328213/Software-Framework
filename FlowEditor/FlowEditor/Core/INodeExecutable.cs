using FlowEditor.Models;
using FlowEditor.Nodes;

namespace FlowEditor.Core;

/// <summary>
/// 执行引擎所需的最小节点执行面。
/// 引擎只面向此接口调度，与 ViewModel 解耦；NodeModel 与 NodeViewModel 均可实现，
/// 未来抽离 FlowCore 程序集时随本接口一起迁移即可。
/// </summary>
public interface INodeExecutable
{
    /// <summary>节点唯一标识。</summary>
    string Id { get; }

    /// <summary>节点显示名称。</summary>
    string Name { get; }

    /// <summary>节点当前执行状态。</summary>
    NodeStatus Status { get; set; }

    /// <summary>最近一次执行耗时（毫秒），由引擎写入。</summary>
    long LastElapsedMs { get; set; }

    /// <summary>执行次数累加，由引擎写入。</summary>
    int Count { get; set; }

    /// <summary>输入端口列表（内置标准端口 + 自定义端口）。</summary>
    IReadOnlyList<PortModel> InputPorts { get; }

    /// <summary>输出端口列表（内置标准端口 + 自定义端口）。</summary>
    IReadOnlyList<PortModel> OutputPorts { get; }

    /// <summary>根据 ID 查找端口（输入或输出）。</summary>
    PortModel? FindPortById(string portId);

    /// <summary>执行节点业务逻辑。</summary>
    Task ExecuteAsync(CancellationToken ct);

    /// <summary>执行前调用：把连线值同步到输入端口绑定的属性。</summary>
    void SyncInputValues();

    /// <summary>执行后调用：把输出端口绑定属性的值同步到端口。</summary>
    void SyncOutputValues();

    /// <summary>引擎在本节点每次执行前调用：清空上一轮激活记录。</summary>
    void ResetActivation();

    /// <summary>显式激活的输出端口名集合；未声明时返回 null（全部出边放行）。</summary>
    IReadOnlyCollection<string>? GetActivatedOutputNames();

    /// <summary>控制流端口名列表；非控制流节点返回 null。</summary>
    IReadOnlyList<string>? ControlFlowPorts { get; }

    /// <summary>多个控制流输入的汇聚策略；普通节点使用全部依赖解析后继续。</summary>
    FlowJoinMode FlowJoinMode { get; }
}

/// <summary>节点具有多个控制流输入时的调度策略。</summary>
public enum FlowJoinMode
{
    /// <summary>兼容模式：等待全部入边解析，任一活动路径到达即可继续。</summary>
    WaitAllAnyActive,

    /// <summary>AND：全部控制流输入都实际完成后才继续。</summary>
    AllActive,

    /// <summary>OR：任一控制流输入实际完成后立即继续。</summary>
    AnyActive
}

/// <summary>
/// 节点容错策略能力接口（P2，可选实现）。
/// 引擎对实现了此接口的节点应用重试/超时/失败策略；
/// 仅实现 INodeExecutable 的节点按默认 FailFast、不重试、不限制超时处理。
/// </summary>
public interface INodeFaultPolicy
{
    /// <summary>失败处理策略。</summary>
    NodeErrorPolicy ErrorPolicy { get; }

    /// <summary>失败自动重试次数（0 = 不重试）。</summary>
    int RetryCount { get; }

    /// <summary>重试间隔（毫秒）。</summary>
    int RetryDelayMs { get; }

    /// <summary>单次执行超时（毫秒），0 = 不限制。</summary>
    int TimeoutMs { get; }
}
