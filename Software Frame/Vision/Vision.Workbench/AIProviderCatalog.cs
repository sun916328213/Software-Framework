// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：AIProviderCatalog。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core.AI。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P6 —— Workbench 侧 AI Provider 注册表（三家切换点，计划 §P6 验收
// 「三家 Provider 可在 Workbench 中切换」）。
//
// 工厂延迟构造（选择时才 new；构造本身不碰 Runtime，见 P6 守卫）；
// 选中后的加载/推理/释放一律经 IAIProvider 统一入口，UI 不碰厂商类型。
// 完整 Provider 选择页是 P9 范围，这里预留装配点。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.AI;

namespace Vision.Workbench;

/// <summary>
/// P6 三家 AI Provider 注册表。
/// </summary>
public static class AIProviderCatalog
{
    /// <summary>三家 Provider（名 → 工厂；延迟构造）。</summary>
    public static IReadOnlyDictionary<string, Func<IAIProvider>> Providers { get; } =
        new Dictionary<string, Func<IAIProvider>>(StringComparer.Ordinal)
        {
            ["Halcon"] = () => new Vision.AI.Halcon.HalconAIProvider(),
            ["Aiditron"] = () => new Vision.AI.Aiditron.AiditronProvider(),
            ["Invi"] = () => new Vision.AI.Invi.InviProvider(),
        };

    /// <summary>全部 Provider 名（选择页列表用）。</summary>
    public static IReadOnlyList<string> ProviderNames => Providers.Keys.ToArray();

    /// <summary>按名创建 Provider；未知名抛 <see cref="KeyNotFoundException"/>。</summary>
    public static IAIProvider Create(string name) => Providers[name]();
}
