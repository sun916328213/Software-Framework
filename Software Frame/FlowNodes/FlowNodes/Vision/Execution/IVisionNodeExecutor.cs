// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：IVisionNodeExecutor、VisionNodeExecutionResult、HalconVisionNodeExecutor。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：HalconDotNet、Vision.Core.Tools、Vision.Tools.Halcon.Sessions。导入不代表所有类型都会在运行时加载。
using HalconDotNet;
using Vision.Core.Tools;
using Vision.Tools.Halcon.Sessions;

namespace FlowNodes.Vision.Execution;

/// <summary>当前 HALCON 节点的执行 seam；不转换图像、参数或工具身份。</summary>
public interface IVisionNodeExecutor
{
    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 Execute。
    /// </summary>
    VisionNodeExecutionResult Execute(HalconToolSession tool, HObject? image);
}

/// <summary>
/// VisionNodeExecutionResult 的类型定义。FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
/// </summary>
public sealed record VisionNodeExecutionResult(int ResultCount, E_ToolState State, string Message);

/// <summary>默认 adapter 保留原工具输入赋值与 Run 调用顺序。</summary>
public sealed class HalconVisionNodeExecutor : IVisionNodeExecutor
{
    internal static HalconVisionNodeExecutor Default { get; } = new();

    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 Execute。
    /// 正文中的关键协作调用包括 Run；具体条件和先后次序见方法体。
    /// </summary>
    public VisionNodeExecutionResult Execute(HalconToolSession tool, HObject? image)
    {
        tool.Image = image;
        tool.Run();
        return new(tool.Results?.Count ?? 0, tool.ToolState, tool.RunMessage);
    }
}
