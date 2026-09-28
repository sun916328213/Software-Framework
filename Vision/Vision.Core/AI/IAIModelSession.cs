using System;

// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / AI」、§6）。
//   §6 原文：「Invi / Aiditron 项目各自拥有模型生命周期、native/SDK 封装、异常与结果转换，不相互引用。」
//   ⇒ 生命周期语义必须由**契约**钉住，不能靠各 Provider 自觉。
//   本文件把仓库在 P10 已经确立的三条契约原样上提到 Core（见 .workbuddy-ai/memory/MEMORY.md「Vision / InvI」）：
//     ① 注册成功后才提交 managed 状态（失败不留「假加载」态）；
//     ② 释放/重置失败要**保留状态并上抛**，不吞异常；
//     ③ Dispose 幂等且不抛（用于 finally 兜底），与 ② 分工明确。
namespace Vision.Core.AI
{
    /// <summary>
    /// 一次模型会话 —— 「某个模型已被加载、可以反复推理」这个状态的载体。
    ///
    /// 生命周期契约（P6 三家 Provider 必须一致）：
    /// <list type="number">
    ///   <item>由 <see cref="IAIProvider.OpenSession"/> 创建；native/SDK 侧加载**成功之后**
    ///     才允许把 <see cref="IsLoaded"/> 置 true（失败时不得留下「看起来加载成功」的会话）。</item>
    ///   <item>用 <see cref="IAIProvider.CloseSession"/> 主动释放：**失败要上抛**，
    ///     并保持 <see cref="IsLoaded"/> 不变，让调用方看得见失败。</item>
    ///   <item><see cref="IDisposable.Dispose"/> 只做兜底清理：**幂等、不抛**。</item>
    /// </list>
    /// ⛔ 会话不暴露任何厂商类型（HObject / Mat / native handle），
    /// 推理入参出参一律走 <see cref="AIInferenceRequest"/> / <c>Vision.Core.Results.ToolResult</c>。
    /// </summary>
    public interface IAIModelSession : IDisposable
    {
        /// <summary>模型标识（通常由 Provider 用模型路径派生；用于日志与「重复加载」判定）。</summary>
        string ModelId { get; }

        /// <summary>该会话承载的模型任务类型。</summary>
        AIModelType ModelType { get; }

        /// <summary>该会话实际可用的能力（可能比 Provider 声明的小，例如缺 GPU 时不含 <see cref="AICapabilities.Gpu"/>）。</summary>
        AICapabilities Capabilities { get; }

        /// <summary>模型当前是否已加载且可用。</summary>
        bool IsLoaded { get; }

        /// <summary>
        /// 执行一次推理。
        ///
        /// 返回类型按 <see cref="ModelType"/> 约定（计划 §6 的结果表）：
        /// <list type="bullet">
        ///   <item><see cref="AIModelType.Classification"/> → <c>AIClassificationResult</c>；</item>
        ///   <item><see cref="AIModelType.Detection"/> → <c>AIDetectionResult</c>；</item>
        ///   <item><see cref="AIModelType.Segmentation"/> → <c>AISegmentationResult</c>；</item>
        ///   <item><see cref="AIModelType.AnomalyDetection"/> → <c>AIAnomalyResult</c>。</item>
        /// </list>
        /// 统一以 <c>ToolResult</c> 为返回类型而不是为每种任务开一个重载：
        /// 调用方是「拿到会话就按 <see cref="ModelType"/> 处理」的 Tool（P5 建立），
        /// 泛型化只会把类型参数一路传染到 Tool 与 UI。
        /// 推理**失败**不抛异常，而是返回 <c>Success == false</c> 的结果 ——
        /// 视觉产线上单帧失败是常态，不该靠异常做流程控制。
        /// </summary>
        /// <param name="request">推理请求。</param>
        /// <returns>该模型类型对应的公共结果。</returns>
        /// <exception cref="InvalidOperationException"><see cref="IsLoaded"/> 为 false 时抛出。</exception>
        Results.ToolResult Run(AIInferenceRequest request);
    }
}
