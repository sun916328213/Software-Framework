using System.Windows.Input;

namespace FlowEditor.Presentation.Commands;

/// <summary>定义依赖流程画布视觉树并沿 WPF 元素树路由的命令。</summary>
public static class FlowChartRoutedCommands
{
    /// <summary>获取导出活动流程画布为 PNG 的路由命令。</summary>
    public static RoutedUICommand ExportPng { get; } = new(
        "导出 PNG",
        nameof(ExportPng),
        typeof(FlowChartRoutedCommands));
}
