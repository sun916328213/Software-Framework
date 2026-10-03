// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：IVisionNodeEditorSource。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core.Tools。导入不代表所有类型都会在运行时加载。
using Vision.Core.Tools;

namespace FlowNodes.Vision.Contracts;

/// <summary>为编辑 adapter 提供已注册的工具；保持名称还原与生命周期归属。</summary>
public interface IVisionNodeEditorSource
{
    /// <summary>准备既有工具实例，不创建窗口或改变工具参数。</summary>
    IEditableTool PrepareToolForEditing();
}
