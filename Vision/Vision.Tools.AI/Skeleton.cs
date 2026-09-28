// ─────────────────────────────────────────────────────────────────────────────
// P1 骨架占位 —— Vision.Tools.AI
// 来源：Vision_Architecture_Optimization_Plan v1.1 §8「P1 - 架构骨架与命名收口」
//
// 本工程在 P1 阶段只建立**项目边界**，不含任何算法实现。
// P1 验收口径：「所有项目可编译；只改边界与引用，不改算法行为」。
//
// 职责（计划 §2、§6）：统一 AI Tool 层 —— 只定义
//   「分类 / 检测 / 分割 / 异常检测这些工具**怎么工作**」。
//   待拆出的 Tool（§P5）：ClassificationTool、DetectionTool、
//   SegmentationTool、AnomalyDetectionTool。
//   Tool 通过 IAIProvider 获取 Session，⛔ 不直接 if/else 判断厂商品牌。
//
// 允许的主要依赖（计划 §2）：**仅 Vision.Core**。
//
// ⛔ 三条硬边界（§3、§6）：
//   1. 不直接引用 Vision.AI.Halcon / Vision.AI.Aiditron / Vision.AI.Invi
//      —— Provider 由 Workbench / 正式应用注入；
//   2. 不引用任何厂商 SDK；
//   3. 公共接口不得暴露 HObject / 厂商原生 Result / native handle（§12）。
//
// 验收目标（§P5）：可使用 fake Provider 完整测试。
//
// 依赖引用在 P5「统一 AI Tool 层」落地时再加。
// ─────────────────────────────────────────────────────────────────────────────

namespace Vision.Tools.AI
{
}
