// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：ICameraProvider。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes、Device.Core.Cameras。导入不代表所有类型都会在运行时加载。
using FlowNodes;
using Device.Core.Cameras;

namespace FlowNodes.Device;

/// <summary>
/// 按名称向流程节点提供相机实例的宿主组合契约。
/// </summary>
/// <remarks>
/// 该接口刻意定义在 FlowNodes 而不是 Device.Core：怎样按名称找到相机属于宿主的组合问题，
/// 不是设备领域契约，放进 Device.Core 会把宿主需求塞进核心层。
/// 接口只包含节点真正需要的能力，不提供枚举硬件、创建相机或维护设备列表的职责。
/// </remarks>
public interface ICameraProvider
{
    /// <summary>当前已配置、可供节点下拉选择的相机名称。</summary>
    IReadOnlyList<string> Names { get; }

    /// <summary>按名称取得相机实例。</summary>
    /// <param name="name">节点配置中选择的相机名称。</param>
    /// <returns>相机实例；是否已完成连接由调用方自行判断。</returns>
    /// <exception cref="KeyNotFoundException">没有名为 <paramref name="name"/> 的相机。</exception>
    ICamera Get(string name);
}
