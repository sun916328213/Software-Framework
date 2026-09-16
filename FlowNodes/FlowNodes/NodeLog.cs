using FlowEditor.Core;
using System;
using System.Threading.Tasks;

namespace FlowNodes
{
    /// <summary>
    /// 节点日志助手：通过 FlowEditor 的结构化日志上下文写入宿主 sink，
    /// 同时输出控制台（ConsoleOutputRouter 捕获进输出面板）。
    /// </summary>
    public static class NodeLog
    {
        /// <summary>写入信息级节点日志。</summary>
        public static Task InfoAsync(string message) => WriteAsync(FlowLogLevel.Information, message);

        /// <summary>写入警告级节点日志。</summary>
        public static Task WarnAsync(string message) => WriteAsync(FlowLogLevel.Warning, message);

        /// <summary>写入错误级节点日志。</summary>
        public static Task ErrorAsync(string message) => WriteAsync(FlowLogLevel.Error, message);

        /// <summary>写入系统错误级节点日志。</summary>
        public static Task SysErrorAsync(string message) => WriteAsync(FlowLogLevel.SysError, message);

        /// <summary>统一发布面板输出与结构化节点日志。</summary>
        private static Task WriteAsync(FlowLogLevel level, string message)
        {
            var node = FlowLogContext.NodeName;
            var prefix = string.IsNullOrEmpty(node) ? "" : $"[{node}] ";
            // 与 Console 捕获路径保持一致：带 #运行序号 [节点名] 前缀
            var run = FlowLogContext.RunId > 0 ? $"#{FlowLogContext.RunId} " : "";
            var text = $"{run}{prefix}{message}";

            // 输出面板（ConsoleOutputRouter 捕获）；抑制 sink，避免与下方结构化写入重复。
            using (FlowLogContext.SuppressSink())
            {
                Console.WriteLine(text);
            }

            FlowLogContext.Write(text, level);
            return Task.CompletedTask;
        }
    }
}
