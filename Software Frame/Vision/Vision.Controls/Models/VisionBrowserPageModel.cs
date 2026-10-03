// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ToolEntry、ProviderEntry、VisionBrowserPageModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel、Vision.Controls.ViewModels、Vision.Core.AI。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;
using Vision.Controls.ViewModels;
using Vision.Core.AI;

#nullable enable
namespace Vision.Controls.Models;

/// <summary>
/// ToolEntry 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// </summary>
public sealed record ToolEntry(string Name, string Backend, Func<(IExecutableTool Card, object Parameters, Action<IAIProvider>? SetProvider)> Create);
/// <summary>
/// ProviderEntry 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// </summary>
public sealed record ProviderEntry(string Name, bool IsAvailable);

/// <summary>浏览器页输入、选择和执行反馈。</summary>
public partial class VisionBrowserPageModel : ObservableObject
{
    public IReadOnlyList<ToolEntry> ToolEntries { get; init; } = [];
    public IReadOnlyList<ProviderEntry> ProviderEntries { get; init; } = [];
    [ObservableProperty] private bool _hasImage;
    [ObservableProperty] private bool _isClosed;
    [ObservableProperty] private ToolCardViewModel? _currentCard;
    [ObservableProperty] private object? _currentParameters;
    [ObservableProperty] private ToolEntry? _selectedEntry;
    [ObservableProperty] private string _selectedProviderName = string.Empty;
    [ObservableProperty] private string _imageInfoText = "无图像（可加载文件或使用合成图）";
    [ObservableProperty] private string _compareText = "—";
    [ObservableProperty] private string _benchmarkText = "—";
}
