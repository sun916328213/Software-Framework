// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：FlowNodeDefaults。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Core.Contracts、ScriptEditor.Runtime.Services。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Core.Contracts;
using ScriptEditor.Runtime.Services;

namespace FlowNodes.Integration;

/// <summary>无参节点构造的兼容组合入口；桌面宿主经节点工厂注入已注册的运行时。</summary>
internal static class FlowNodeDefaults
{
    // 延迟获取，保持旧节点在首次执行前不创建 Roslyn 运行时的行为。
    internal static IScriptRuntime GetScriptRuntime() => RoslynScriptRuntime.Shared;
}
