using Vision.Controls.ViewModels;
using Vision.Core.AI;

#nullable enable
namespace Vision.Controls.Models;

/// <summary>
/// 工具工厂与可选编辑能力；界面不根据 Name 或 Backend 推断入口。
/// </summary>
/// <remarks>SupportsModelLoading 显式提供模型准备入口，与可选 Provider 注入相互独立。</remarks>
public sealed record ToolEntry(string Name, string Backend,
    Func<(IExecutableTool Card, object Parameters, Action<IAIProvider>? SetProvider)> Create,
    Action<object, Vision.Core.Imaging.VisionImage>? SetTemplate = null,
    bool IsDefault = false,
    bool SupportsModelLoading = false)
{
    /// <summary>节点属性显式白名单；只展示这些原DTO字段，不展开整个工具或会话。</summary>
    public IReadOnlyList<string> NodeParameterNames { get; init; } = [];
    public IReadOnlyList<string> NumericResultFields { get; init; } = [];
    public IReadOnlyList<string> LineResultFields { get; init; } = [];
    public IReadOnlyList<string> CircleResultFields { get; init; } = [];
    public IReadOnlyList<string> PointResultFields { get; init; } = [];
    public IReadOnlyList<string> BooleanResultFields { get; init; } = [];
    public bool ProducesOutputImage { get; init; }
    public bool ProducesFixture { get; init; }
}
