using DataManager.Log;
using Extension.Struct;
using FlowEditor.Core;
using System;
using System.Threading.Tasks;

namespace FlowNodes
{
    /// <summary>
    /// 节点日志助手：按当前流程解析 DataManager 的 ILog（LogManager）写入对应流程日志文件，
    /// 同时输出控制台（ConsoleOutputRouter 捕获进输出面板）。Resolver 由宿主在启动时注册。
    /// </summary>
    public static class NodeLog
    {
        /// <summary>按流程名解析 ILog（宿主注册：container.Resolve&lt;LogManager&gt;()[name]）。</summary>
        public static Func<string, ILog?>? Resolver { get; set; }

        public static Task Info(string message) => Write(ELogLevel.Information, message);
        public static Task Warn(string message) => Write(ELogLevel.Warning, message);
        public static Task Error(string message) => Write(ELogLevel.Error, message);

        private static async Task Write(ELogLevel level, string message)
        {
            var node = FlowLogContext.NodeName;
            var prefix = string.IsNullOrEmpty(node) ? "" : $"[{node}] ";
            // 与 Console 捕获路径保持一致：带 #运行序号 [节点名] 前缀
            var run = FlowLogContext.RunId > 0 ? $"#{FlowLogContext.RunId} " : "";
            var text = $"{run}{prefix}{message}";

            // 输出面板（ConsoleOutputRouter 捕获）；抑制 FileSink，避免与下方落盘重复
            using (FlowLogContext.SuppressFileSink())
            {
                Console.WriteLine(text);
            }

            try
            {
                // 按当前「流程名\任务名」写入 DataManager 的 ILog（LogManager），保留分级
                var key = FlowLogContext.LogKey;
                if (string.IsNullOrEmpty(key)) return;
                var log = Resolver?.Invoke(key);
                if (log != null)
                    await log.AddAsync(text, level);
            }
            catch
            {
                // 日志失败不影响节点执行
            }
        }
    }
}
