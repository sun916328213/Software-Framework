// ─────────────────────────────────────────────────────────────────────────────
// P1 骨架占位 —— Vision.Tools.OpenCv
// 来源：Vision_Architecture_Optimization_Plan v1.1 §8「P1 - 架构骨架与命名收口」
//
// 本工程在 P1 阶段只建立**项目边界**，不含任何算法实现。
// P1 验收口径：「所有项目可编译；只改边界与引用，不改算法行为」。
//
// 职责（计划 §2）：OpenCV 视觉工具 —— OpenCV 是**正式 Tool 后端**，
//   不是临时 Helper，也不要求与 HALCON 1:1（§1、§5.3）。
//   建议内部结构（§5.3）：Processing / Matching / Inspection / Measurement
//   第一批建议实现（§5.3）：Threshold、Morphology、Blob、TemplateMatch、
//   FindLine / FindCircle。
//
// 允许的主要依赖（计划 §2）：Vision.Core、OpenCvSharp。
//
// 验收目标（§P4）：OpenCV 能作为正式后端独立部署，**不依赖 HALCON**。
//   ⇒ 本工程 ⛔ 不得引用 DPLibrary_Halcon_F48，也不得经它拿到 OpenCvSharp。
//   （注意：OpenCvSharp4 目前仍由 Extension.csproj 引入，属 P4 待清理项。）
//
// 依赖引用在 P4「OpenCV Tool 首批能力」落地时再加。
// ─────────────────────────────────────────────────────────────────────────────

namespace Vision.Tools.OpenCv
{
}
