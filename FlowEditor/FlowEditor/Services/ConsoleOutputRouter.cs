using System;
using System.IO;
using System.Text;
using System.Threading;
using Extension.Struct;
using FlowEditor.Core;

namespace FlowEditor.Services;

/// <summary>
/// 全局 Console 输出路由：将流程执行期间节点代码中的 Console.WriteLine 捕获并转发到输出面板。
/// 与脚本编辑器的输出方式保持一致（AsyncLocal 路由，多流程输出互不干扰）。
/// </summary>
public static class ConsoleOutputRouter
{
    private sealed class RoutingConsoleWriter : TextWriter
    {
        private readonly TextWriter _previous;
        private static readonly AsyncLocal<Action<string>?> Current = new();

        /// <summary>创建保留原始输出目标的路由写入器。</summary>
        public RoutingConsoleWriter(TextWriter previous) => _previous = previous;

        public override Encoding Encoding => _previous.Encoding;

        /// <summary>字符写入保持原控制台语义，不参与按行路由。</summary>
        public override void Write(char value) => _previous.Write(value);

        /// <summary>无换行字符串保持原控制台语义，不提前发布不完整日志。</summary>
        public override void Write(string? value) => _previous.Write(value);

        /// <summary>写入原控制台，并把完整行按当前异步流程上下文转发。</summary>
        public override void WriteLine(string? value)
        {
            _previous.WriteLine(value);
            var msg = value ?? string.Empty;

            // 消息已带 #运行序号 前缀（如 NodeLog 显式构造）：面板原样转发，文件由原路径（ILog）落盘
            if (msg.StartsWith("#"))
            {
                Current.Value?.Invoke(msg);
                return;
            }

            // 流程作用域内的裸 Console 输出（如 LogNode 的 Console.WriteLine）：
            // 统一补 #运行序号 [节点名] 前缀，面板与文件格式一致（默认信息级）
            var line = FlowLogContext.Format(msg);
            if (line != null)
            {
                Current.Value?.Invoke(line);
                if (!FlowLogContext.IsFileSinkSuppressed)
                    FlowLogContext.FileSink?.Invoke(line, ELogLevel.Information);
            }
            else
            {
                Current.Value?.Invoke(msg);
            }
        }

        /// <summary>为当前异步调用链设置输出接收器，并返回恢复旧接收器的作用域。</summary>
        public static IDisposable EnterScope(Action<string> onLine)
        {
            var old = Current.Value;
            Current.Value = onLine;
            return new Scope(old);
        }

        /// <summary>保存进入前的接收器，以便作用域结束时恢复嵌套路由。</summary>
        private sealed class Scope(Action<string>? old) : IDisposable
        {
            /// <summary>恢复进入当前作用域前的控制台行接收器。</summary>
            public void Dispose() => Current.Value = old;
        }
    }

    private static bool _installed;

    /// <summary>进入路由作用域：作用域内 Console.WriteLine 的每一行都会转发给 onLine。</summary>
    public static IDisposable EnterScope(Action<string> onLine)
    {
        if (!_installed)
        {
            _installed = true;
            Console.SetOut(new RoutingConsoleWriter(Console.Out));
        }
        return RoutingConsoleWriter.EnterScope(onLine);
    }
}
