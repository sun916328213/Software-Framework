// ─────────────────────────────────────────────────────────────────────────────
// P1 骨架占位 —— Vision.AI.Invi
// 来源：Vision_Architecture_Optimization_Plan v1.1 §8「P1 - 架构骨架与命名收口」
//
// 本工程在 P1 阶段只建立**项目边界**，不含任何算法实现。
// P1 验收口径：「所有项目可编译；只改边界与引用，不改算法行为」。
//
// 职责（计划 §2、§6）：业视（Invi）AI Provider ——
//   迁移 InviHelper、IInviNativeApi、DynamicDllInviNativeApi（§P6），
//   并保留现有生命周期测试思路。自己拥有模型生命周期、native/SDK 封装、
//   异常与结果转换。
//
// 允许的主要依赖（计划 §2）：Vision.Core + Invi SDK / Native DLL。
//
// ⛔ 与 Vision.AI.Halcon / Vision.AI.Aiditron **互不引用**（§6）。
// ⛔ 公共接口不得暴露 native handle 或厂商原生 Result（§12）。
//
// 迁移来源（当前状态，见 Vision/OPTIMIZATION.md 与 P10 收口结果）：
//   Vision/DPLibrary_Halcon_F48/Tools/AI/Invi/{InviHelper, IInviNativeApi,
//   DynamicDllInviNativeApi}.cs —— 现有正式链为 AITool -> InviHelper -> INVI API.dll。
//
// 验收目标（§P6）：加载 / 推理 / 释放行为独立验证。
//
// 依赖引用在 P6「三套 AI Provider 迁移」落地时再加。
// ─────────────────────────────────────────────────────────────────────────────

namespace Vision.AI.Invi
{
}
