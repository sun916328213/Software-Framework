// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ConfigRoot。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.IO。导入不代表所有类型都会在运行时加载。
using System;
using System.IO;

namespace DataManager.Controls.Configuration
{
    /// <summary>
    /// 配置根路径 seam：页面模型与工作区从它取 <c>FilePath</c>，而不是各自硬编码
    /// <c>AppDomain.CurrentDomain.BaseDirectory</c>。
    /// 生产默认 = <see cref="Default"/>（与改造前逐字一致的 <c>&lt;BaseDirectory&gt;\Config</c>，机型默认位置不变）；
    /// 测试与工作台会话用 <see cref="CreateTemporary"/> 或直接构造，拿到自己的临时目录，
    /// 从而不再读写共享 Config、也不互相覆盖。
    /// 刻意做成可注入的对象而不是静态可变量：并行会话天然隔离。
    /// </summary>
    public sealed class ConfigRoot
    {
        /// <summary>配置目录（写入页面模型 FilePath 的值）。</summary>
        public string Directory { get; }

        /// <summary>
        /// 用指定目录构造。目录本身可以不存在——由保存方按需创建，与改造前行为一致。
        /// </summary>
        /// <exception cref="ArgumentException">目录为空或全为空白。</exception>
        public ConfigRoot(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("配置根目录不能为空", nameof(directory));

            Directory = directory;
        }

        /// <summary>生产默认根：<c>&lt;AppDomain.BaseDirectory&gt;\Config</c>（即改造前各页面模型各自的取值）。</summary>
        public static ConfigRoot Default { get; } =
            new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config"));

        /// <summary>
        /// 在系统临时目录下生成一个唯一命名的根，供测试或工作台场景隔离使用。
        /// 只返回路径、不创建目录；调用方负责在用完后删除整棵目录。
        /// </summary>
        public static ConfigRoot CreateTemporary() =>
            new(Path.Combine(Path.GetTempPath(), "SF-Config-" + Guid.NewGuid().ToString("N")));
    }
}
