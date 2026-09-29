namespace FlowEditor.Core;

/// <summary>
/// 节点验证能力（P1）。每个节点可实现此接口，在流程保存、部署、启动前接受整图验证，
/// 提前发现缺设备、缺输入、参数错误、缺插件等问题，而不等到执行一半才失败。
/// <para/>
/// 未实现此接口的旧节点按验证通过处理，零改动兼容。
/// 验证只读：实现不得执行设备动作、不得修改节点状态、不得产生副作用。
/// </summary>
public interface IFlowNodeValidatable
{
    /// <summary>校验节点自身配置（不依赖连线），返回全部可发现问题。</summary>
    /// <param name="ctx">验证上下文（含节点标识、服务与变量快照）。</param>
    Task<IEnumerable<FlowDiagnostic>> ValidateAsync(NodeValidationContext ctx);
}
