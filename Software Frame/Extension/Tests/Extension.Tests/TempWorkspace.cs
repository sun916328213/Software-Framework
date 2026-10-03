// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：TempWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.IO。导入不代表所有类型都会在运行时加载。
using System;
using System.IO;

namespace Extension.Tests
{
    /// <summary>
    /// 一次性临时工作目录：构造时创建，<see cref="Dispose"/> 时递归删除。
    /// <para>
    /// 专供 <c>Core/Files</c>、<c>Core/Data</c> 这类**必须真落盘**的 T0 用例使用。
    /// 这些用例验证的对象就是「文件系统副作用」本身（<c>.bak</c> 轮换、按目录分组、
    /// gzip 往返、路径字符串替换），换成内存替身（如 <c>System.IO.Abstractions</c>）
    /// 会把要测的东西一起替掉 ⇒ 测不到真实行为。
    /// </para>
    /// <para>
    /// 路径一律带随机 GUID 后缀，保证并行运行的用例互不干扰；
    /// 清理失败只吞 <see cref="IOException"/> / <see cref="UnauthorizedAccessException"/>
    /// （临时目录由 OS 兜底回收），不让清理动作污染断言结果。
    /// </para>
    /// </summary>
    internal sealed class TempWorkspace : IDisposable
    {
        /// <summary>临时根目录的绝对路径（不含尾分隔符）。</summary>
        public string Root { get; }

        /// <summary>
        /// 初始化 TempWorkspace 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Combine、GetTempPath、ToString、NewGuid、CreateDirectory；具体条件和先后次序见方法体。
        /// </summary>
        public TempWorkspace(string tag)
        {
            Root = Path.Combine(Path.GetTempPath(), tag + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>在根目录下拼一个路径（只拼，不创建任何东西）。</summary>
        public string Combine(params string[] segments)
        {
            string result = Root;
            foreach (string segment in segments)
            {
                result = Path.Combine(result, segment);
            }
            return result;
        }

        /// <summary>写一个文件，自动创建父目录，返回完整路径。</summary>
        public string WriteFile(string relativePath, string content)
        {
            string full = Path.Combine(Root, relativePath);
            string parent = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }
            File.WriteAllText(full, content);
            return full;
        }

        /// <summary>
        /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Exists、Delete；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
