// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：AIModelParameters。成员说明可结合下方具体实现阅读。
// ─────────────────────────────────────────────────────────────────────────────
// P5 —— 统一 AI 参数（计划 §7「Parameters：运行参数与训练参数显式建模」）。
//
// 旧 AITool 把品牌/任务/阈值/尺寸/面积全塞进一个大类（AIBrand + AIToolType 膨胀，
// 计划 §6 点名要拆）。新模型：
//   · AIModelParameters：四 Tool 共用的模型与推理参数（路径/设备/阈值/数量）；
//   · 各任务 extras（MinArea 等）放在各自的 *Parameters 里，不回流基类。
// 全部公有 setter，JSON 可往返（与 Core 契约测试同口径）。
// ─────────────────────────────────────────────────────────────────────────────

namespace Vision.Tools.AI;

/// <summary>
/// AI 模型与推理公共参数（四 Tool 共用）。
/// </summary>
public class AIModelParameters
{
    /// <summary>模型文件 / 模型目录路径（各 Provider 自行解释）。</summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>设备标识（如 "cpu"、"gpu0"）；空表示由 Provider 决定。</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>置信度阈值（0~1）；≤0 表示用 Provider 默认值。</summary>
    public double ScoreThreshold { get; set; }

    /// <summary>最多返回多少个结果；≤0 表示不限制。</summary>
    public int MaxResults { get; set; }
}
