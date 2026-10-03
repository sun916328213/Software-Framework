// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：IAIProvider。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core。导入不代表所有类型都会在运行时加载。
using Vision.Core;
// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / AI」、§6）。
//   §6 的依赖方向是单向的：
//        Vision.Tools.AI ──► IAIProvider ──► Vision.AI.Halcon / Vision.AI.Aiditron / Vision.AI.Invi
//   即 Tool 只认识 IAIProvider，**不写 if/else 判断厂商品牌**
//   （§P5 验收：「Tool 通过 IAIProvider 获取 Session；不直接 if/else 判断厂商品牌」）。
namespace Vision.Core.AI
{
    /// <summary>
    /// AI 运行时提供方 —— 一家 SDK 一个实现（Halcon / Aiditron / Invi）。
    ///
    /// 实现位置：<c>Vision.AI.Halcon</c> / <c>Vision.AI.Aiditron</c> / <c>Vision.AI.Invi</c>，
    /// 三家**互不引用**（计划 §6）。Core 只定义这个接口，不引任何一家 SDK。
    /// </summary>
    public interface IAIProvider
    {
        /// <summary>提供方名称（如 "Halcon" / "Aiditron" / "Invi"），用于 UI 选择与日志。</summary>
        string Name { get; }

        /// <summary>该提供方声明支持的能力；实际可用性还要结合 <see cref="IsAvailable"/>。</summary>
        AICapabilities Capabilities { get; }

        /// <summary>
        /// 运行时是否就绪（SDK / native DLL / License / 设备都到位）。
        /// ⛔ 允许为 false —— 这是**正常状态**：计划 §P10 验收要求
        /// 「普通 CI 在没有真实 HALCON AI / Aiditron / Invi Runtime、GPU 和模型环境时仍可完成核心测试」，
        /// 所以调用方必须能用「Provider 不可用」这条分支走通全流程（P5 用 fake Provider 测）。
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 打开一个模型会话。
        /// ⛔ 失败语义：native 侧加载**成功之后**才允许返回会话；加载失败要么抛异常，
        /// 要么返回 null，**不得**返回一个 <see cref="IAIModelSession.IsLoaded"/> 为 false
        /// 却「看起来可用」的会话（P10 已确立的契约，见 <see cref="IAIModelSession"/> 注释）。
        /// </summary>
        /// <param name="request">模型请求。</param>
        /// <returns>已加载的会话。</returns>
        IAIModelSession OpenSession(AIModelRequest request);

        /// <summary>
        /// 主动释放会话。
        /// ⛔ 与 <see cref="System.IDisposable.Dispose"/> 的分工：本方法**失败要上抛**并保留
        /// <see cref="IAIModelSession.IsLoaded"/> 状态（调用方需要看见失败并决定重试或报警）；
        /// Dispose 只做幂等兜底、不抛。
        /// </summary>
        /// <param name="session">要释放的会话；null 视为无操作。</param>
        void CloseSession(IAIModelSession session);
    }
}
