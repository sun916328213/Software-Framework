using System;
using System.IO;
using System.Text;
using System.Threading;

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

        public RoutingConsoleWriter(TextWriter previous) => _previous = previous;

        public override Encoding Encoding => _previous.Encoding;

        public override void Write(char value) => _previous.Write(value);

        public override void Write(string? value) => _previous.Write(value);

        public override void WriteLine(string? value)
        {
            _previous.WriteLine(value);
            Current.Value?.Invoke(value ?? string.Empty);
        }

        public static IDisposable EnterScope(Action<string> onLine)
        {
            var old = Current.Value;
            Current.Value = onLine;
            return new Scope(old);
        }

        private sealed class Scope(Action<string>? old) : IDisposable
        {
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
