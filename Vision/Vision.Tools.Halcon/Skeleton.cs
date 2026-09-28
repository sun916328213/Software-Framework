// ─────────────────────────────────────────────────────────────────────────────
// P1 骨架占位 —— Vision.Tools.Halcon
// 来源：Vision_Architecture_Optimization_Plan v1.1 §8「P1 - 架构骨架与命名收口」
//
// 本工程在 P1 阶段只建立**项目边界**，不含任何算法实现。
// P1 验收口径：「所有项目可编译；只改边界与引用，不改算法行为」。
//
// 职责（计划 §2）：传统 HALCON 视觉工具。
//   建议内部结构（§5.2）：Acquisition / Processing / Matching / Location /
//   Inspection / Measurement / Identification / Calibration / Coordinate / Internal
//   待迁移的 Tool：Blob、Caliper、ShapeMatch、FindLine、FindCircle、
//   Metrology、OCR、Barcode、Calibration、Fixture。
//
// 允许的主要依赖（计划 §2）：
//   · Vision.Core
//   · HalconDotNet —— ⛔ 必须经 Extension.csproj 这一唯一入口，
//     不得在本工程写 <Reference Include="halcondotnet"> 或重复锁 Halcon 包版本。
//
// ⛔ 本工程**不得**承载 HALCON AI（AI Runtime 归 Vision.AI.Halcon，§5.2）；
//    两者互不依赖（§3）。
//
// 依赖引用在 P3「HALCON 传统 Tool 迁移与标准化」落地时再加，
// 避免空壳阶段无端扩大还原面与警告面。
// ─────────────────────────────────────────────────────────────────────────────

namespace Vision.Tools.Halcon
{
}
