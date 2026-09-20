using System.IO;
using Extension.Core.Conversion;

namespace Communication.Workbench.Infrastructure;

/// <param name="Title">侧栏显示标题。</param>
/// <param name="Badge">侧栏徽标文字。</param>
/// <param name="ViewName">Prism 视图注册名。</param>
public sealed record PageItem(
    string Title = "",
    string Badge = "",
    string ViewName = "");

/// <summary>
/// 工作台导航页配置：独立文件 <程序目录>/Config/Pages.json，与 DataManager.Workbench 同一约定。
/// 对外接口只有 CommunicationNew 一套，故默认页只有一项 —— 需要增页时改配置，不改壳。
/// 缺文件、坏值一律回退 <see cref="DefaultPages"/>，不抛。
/// </summary>
public static class NavPageConfig
{
    /// <summary>导航页配置文件完整路径。</summary>
    public static string FileFullPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Pages.json");

    /// <summary>无配置时的默认页（缺配置、坏配置、空页都回退到这里）。</summary>
    public static IReadOnlyList<PageItem> DefaultPages { get; } =
    [
        new("通讯设备测试", "COMM", "CommunicationView"),
    ];

    /// <summary>读配置页；缺文件/坏文件返回 null，由调用方回退 <see cref="DefaultPages"/>，不抛。</summary>
    public static List<PageItem>? Read() => JsonEx.TryRead<List<PageItem>>(FileFullPath);
}
