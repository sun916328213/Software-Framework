using System.Collections.Generic;
using System.IO;
using System.Linq;
using Extension.Core.Conversion;

namespace DataManager.Workbench.Infrastructure
{
    /// <summary>导航页配置（视图名须与 Prism 导航注册名一致）。</summary>
    /// <param name="Title">侧栏显示标题。</param>
    /// <param name="Badge">侧栏徽标文字。</param>
    /// <param name="ViewName">Prism 视图注册名。</param>
    public sealed record PageItem(
        string Title = "",
        string Badge = "",
        string ViewName = "");

    /// <summary>
    /// 工作台导航页配置：独立文件 <程序目录>/Config/Pages.json，
    /// 与 Sql/Log/Excel 各页「一页一文件」的落盘约定保持一致。
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
            new("SQL 数据库测试", "SQL", "SqlView"),
            new("日志测试", "LOG", "LogView"),
            new("Excel 测试", "XLS", "ExcelView"),
        ];

        /// <summary>读配置页；缺文件/坏文件返回 null，由调用方回退 <see cref="DefaultPages"/>，不抛。</summary>
        public static List<PageItem>? Read() => JsonEx.TryRead<List<PageItem>>(FileFullPath);

        /// <summary>写配置页（目录不存在自动建）。</summary>
        public static void Write(IEnumerable<PageItem> pages) =>
            JsonEx.Save(pages.ToList(), FileFullPath);
    }
}
