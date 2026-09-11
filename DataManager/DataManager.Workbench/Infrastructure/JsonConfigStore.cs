using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DataManager.Workbench.Infrastructure
{
    /// <summary>
    /// 工作台主题 / 导航配置持久化（BaseDirectory/workbench-config.json），外加一页一文件的
    /// 档案读写（Config/*.json，供 ConfigManager 控件保存按钮与各页 ViewModel 落盘/读取共用）。
    /// 缺文件、缺节、坏值回退默认，不抛。
    /// </summary>
    public static class JsonConfigStore
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        public sealed record ThemeSection(
            bool IsDark = false,
            string Primary = "Indigo",
            string Secondary = "Teal");

        /// <summary>导航页配置（Prism 视图名须与 RegisterForNavigation 注册名一致）。</summary>
        public sealed record PageItem(
            string Title = "",
            string Badge = "",
            string ViewName = "");

        public static IReadOnlyList<PageItem> DefaultPages { get; } =
        [
            new("SQL 数据库测试", "SQL", "SqlView"),
            new("日志测试", "LOG", "LogView"),
            new("Excel 测试", "XLS", "ExcelView"),
        ];

        public static string FileFullPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workbench-config.json");

        public static void Save<T>(string section, T data)
        {
            JsonObject root;
            try
            {
                root = File.Exists(FileFullPath)
                    ? JsonNode.Parse(File.ReadAllText(FileFullPath))?.AsObject() ?? new JsonObject()
                    : new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }

            root[section] = JsonSerializer.SerializeToNode(data, Options);
            Directory.CreateDirectory(Path.GetDirectoryName(FileFullPath)!);
            File.WriteAllText(FileFullPath, root.ToJsonString(Options));
        }

        public static T? Load<T>(string section)
        {
            try
            {
                if (!File.Exists(FileFullPath))
                    return default;
                var root = JsonNode.Parse(File.ReadAllText(FileFullPath))?.AsObject();
                var node = root?[section];
                return node is null ? default : node.Deserialize<T>(Options);
            }
            catch
            {
                return default;
            }
        }

        /// <summary>读一页一文件的档案列表：缺文件/坏文件回 null，由调用方回退默认，不抛。</summary>
        public static List<T>? ReadList<T>(string path)
        {
            try
            {
                return Extension.Core.ConvertEx.JsonEx.Read<List<T>>(path);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>写一页一文件的档案列表（自动建目录）。</summary>
        public static void WriteList<T>(string path, IEnumerable<T> items)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            Extension.Core.ConvertEx.JsonEx.Save(items.ToList(), path);
        }

        /// <summary>
        /// 清掉老 workbench-config.json 里已迁走的节（Theme/Pages 保留），不抛。
        /// </summary>
        public static void RemoveLegacySections(params string[] sections)
        {
            try
            {
                if (!File.Exists(FileFullPath) || sections.Length == 0)
                    return;
                var root = JsonNode.Parse(File.ReadAllText(FileFullPath))?.AsObject();
                if (root is null)
                    return;
                var changed = false;
                foreach (var s in sections)
                    changed |= root.Remove(s);
                if (changed)
                    File.WriteAllText(FileFullPath, root.ToJsonString(Options));
            }
            catch
            {
                // 老文件坏了就不管，迁移后新文件为准。
            }
        }
    }
}
