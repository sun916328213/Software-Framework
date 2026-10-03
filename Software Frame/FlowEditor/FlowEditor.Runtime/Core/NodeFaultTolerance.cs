// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：RetryBoundary。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using FlowEditor.Core;
namespace FlowEditor.Runtime;

/// <summary>
/// 重试边界（P2）。三层各管一层，禁止多层重试指数放大：
/// <list type="number">
/// <item>L0 协议级恢复：通信/设备/SDK 适配器内部只做单次传输恢复（重连一次、单次读超时），
/// 不得循环重试业务动作； terminal 结果用 <see cref="FlowNodeException"/> 分类上报。</item>
/// <item>L1 业务动作重试：引擎按 <see cref="INodeFaultPolicy"/>（RetryCount/RetryDelayMs）执行，
/// 仅当失败可重试（<see cref="FlowNodeException.IsRetryable"/>）时继续；配置/视觉/取消类失败直接终结本轮重试。</item>
/// <item>L2 流程容错：<see cref="NodeErrorPolicy"/> 决定失败后流程继续还是终止，不触发新的业务重试。</item>
/// </list>
/// </summary>
public static class RetryBoundary
{
    /// <summary>判断失败后是否允许开始下一次业务重试。</summary>
    /// <param name="failure">本轮失败（null 表示成功）。</param>
    /// <param name="attempt">刚完成的 attempt 序号（≥1）。</param>
    /// <param name="maxAttempts">最大 attempt 数（1 + RetryCount）。</param>
    /// <returns>true 表示可继续重试；false 表示终结重试、进入容错处理。</returns>
    public static bool ShouldRetry(Exception? failure, int attempt, int maxAttempts)
    {
        if (failure == null) return false;
        if (attempt >= maxAttempts) return false;
        // L1 规则：明确不可重试的分类失败（配置/视觉/取消）不再浪费重试次数。
        if (failure is FlowNodeException flow && !flow.IsRetryable) return false;
        return true;
    }

    /// <summary>提取失败的稳定错误码；未知异常返回 null（遥测只记录已知分类）。</summary>
    public static string? GetErrorCode(Exception? failure) =>
        failure is FlowNodeException flow ? flow.ErrorCode : null;
}
