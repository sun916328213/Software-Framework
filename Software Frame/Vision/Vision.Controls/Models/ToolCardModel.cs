// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ToolCardModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;
namespace Vision.Controls.Models;
/// <summary>
/// ToolCardModel 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
/// </summary>
public partial class ToolCardModel : ObservableObject
{
    public string ToolName { get; set; } = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isValid;
    [ObservableProperty] private long _elapsedMs;
    [ObservableProperty] private string _resultSummary = string.Empty;
}
