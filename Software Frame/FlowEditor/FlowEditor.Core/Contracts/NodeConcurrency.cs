// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeExecutionAffinity、NodeConcurrencyMode、INodeExecutionAffinity、INodeConcurrency。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core;

/// <summary>
/// 节点执行亲和性（P1）。节点声明自己的执行特征，由 Runtime Scheduler 决定如何调度；
/// 禁止用 "async + Task.CompletedTask" 掩盖同步阻塞。
/// </summary>
public enum NodeExecutionAffinity
{
    /// <summary>普通纯计算/轻量逻辑，无特殊调度要求。</summary>
    Any = 0,

    /// <summary>默认后台执行（I/O、普通设备调用）。</summary>
    Background = 1,

    /// <summary>厂商 SDK 需要固定线程或串行线程（如部分 Halcon/相机 SDK）；Runtime 保证串行。</summary>
    Dedicated = 2,

    /// <summary>仅设计时/极少数 UI 动作；Runtime 节点原则上不得使用。</summary>
    UI = 3,
}

/// <summary>
/// 节点并发模式（P1）。Runtime 统一调度，避免每个 Node 自己 lock。
/// </summary>
public enum NodeConcurrencyMode
{
    /// <summary>可重入：任意并发执行（默认，保持现有行为）。</summary>
    Reentrant = 0,

    /// <summary>实例互斥：同一节点实例不可并发（循环重入、并行分支同时命中时串行）。</summary>
    InstanceExclusive = 1,

    /// <summary>资源互斥：持有相同资源键的节点全局串行（如 Camera:Cam01、PLC:PLC01）。</summary>
    ResourceExclusive = 2,
}

/// <summary>声明节点执行亲和性的能力接口（P1，可选实现；未实现按 Background 处理）。</summary>
public interface INodeExecutionAffinity
{
    /// <summary>获取节点执行亲和性。</summary>
    NodeExecutionAffinity Affinity { get; }
}

/// <summary>声明节点并发与资源锁需求的能力接口（P1，可选实现；未实现按 Reentrant 处理）。</summary>
public interface INodeConcurrency
{
    /// <summary>获取节点并发模式。</summary>
    NodeConcurrencyMode ConcurrencyMode { get; }

    /// <summary>
    /// 返回本节点占用的资源键（如 "Camera:Cam01"、"PLC:PLC01"、"VisionTool:Blob01"）。
    /// ResourceExclusive 模式下，相同键全局串行、不同键可并行；其余模式可返回空。
    /// </summary>
    IEnumerable<string> GetResourceKeys() => [];
}
