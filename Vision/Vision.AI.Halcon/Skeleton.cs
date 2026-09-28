// ─────────────────────────────────────────────────────────────────────────────
// P1 骨架占位 —— Vision.AI.Halcon
// 来源：Vision_Architecture_Optimization_Plan v1.1 §8「P1 - 架构骨架与命名收口」
//
// 本工程在 P1 阶段只建立**项目边界**，不含任何算法实现。
// P1 验收口径：「所有项目可编译；只改边界与引用，不改算法行为」。
//
// 职责（计划 §2、§6）：HALCON AI Provider —— 模型加载、执行推理、释放资源、
//   把 HALCON 原生结果转换成 Core 的公共结果语义。
//
// 允许的主要依赖（计划 §2）：Vision.Core + HALCON AI / HalconDotNet。
//   HalconDotNet ⛔ 必须经 Extension.csproj 这一唯一入口。
//
// ⛔ 为什么单独建工程（§6）：可避免「需要传统 HALCON Tool 但不需要 AI Runtime」
//    的项目被迫携带 AI 依赖。⇒ 本工程与 Vision.Tools.Halcon **互不依赖**（§3）：
//    前者是传统视觉，后者是 HALCON AI Runtime。
//
// 验收目标（§P6）：三家 Provider 可在 Workbench 中切换；
//   加载 / 推理 / 释放行为独立验证。
//
// 依赖引用在 P6「三套 AI Provider 迁移」落地时再加。
// ─────────────────────────────────────────────────────────────────────────────

namespace Vision.AI.Halcon
{
}
