// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：VisionBrowserPageModel。工具描述与Provider描述分别在各自模型文件维护。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel、Vision.Controls.ViewModels、Vision.Core.AI。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;
using Vision.Controls.ViewModels;
using Vision.Core.AI;

#nullable enable
namespace Vision.Controls.Models;

/// <summary>完整编辑器在单工具调试状态之上增加目录与工程状态。</summary>
public partial class VisionBrowserPageModel : VisionToolEditorModel
{
    public IReadOnlyList<ToolEntry> ToolEntries { get; init; } = [];
    [ObservableProperty] private string? _currentProjectPath;
    [ObservableProperty] private bool _isLegacyProject;
    [ObservableProperty] private ToolEntry? _selectedEntry;
    [ObservableProperty] private string _compareText = "—";
}
