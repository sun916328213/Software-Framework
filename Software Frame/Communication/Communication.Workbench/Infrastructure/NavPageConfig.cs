// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：PageItem、NavPageConfig。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Workbench、System.IO、Extension.Core.Conversion。导入不代表所有类型都会在运行时加载。
using Communication.Workbench;
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
