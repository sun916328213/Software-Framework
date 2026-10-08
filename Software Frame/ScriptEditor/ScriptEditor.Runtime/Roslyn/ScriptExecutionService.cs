// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptExecutionService、RoutingConsoleWriter、Scope、StreamingTextWriter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Concurrent、System.Collections.Generic、System.IO、System.Linq、System.Reflection、System.Text。导入不代表所有类型都会在运行时加载。
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting.Hosting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using RoslynPad.Roslyn;
using ScriptEditor.Core.Options;

namespace ScriptEditor.Runtime.Services;

/// <summary>
/// 脚本执行引擎，封装 Roslyn 脚本的编译、执行和输出格式化。
/// </summary>
/// <remarks>
/// <para>纯静态服务，无状态，所有方法为同步或异步纯函数。</para>
/// <para>核心能力：</para>
/// <list type="bullet">
///   <item>脚本编译与返回值检测（CompileAndCheck）</item>
///   <item>异步执行并捕获 Console 输出（RunScriptAsync）</item>
///   <item>并发安全的输出路由（AsyncLocal + RoutingTextWriter）</item>
///   <item>脚本模板自动调用注入（EnsureScriptRunInvoked）</item>
///   <item>调试模式支持（EmitDebugInformation + 源文件映射）</item>
/// </list>
/// </remarks>
/// <author>ScriptEditor Team</author>
/// <version>1.1</version>
public static class ScriptExecutionService
{
    // ════════════════════════════════════════════════════════════════
    //  静态字段
    // ════════════════════════════════════════════════════════════════

    /// <summary>Roslyn 内部方法 HasSubmissionResult 的反射缓存。</summary>
    private static readonly MethodInfo HasSubmissionResultMethod =
        typeof(Compilation).GetMethod("HasSubmissionResult",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new MissingMemberException("HasSubmissionResult 方法未找到，Roslyn 版本可能不兼容");

    /// <summary>C# 对象格式化选项（成员分行显示）。</summary>
    private static readonly PrintOptions PrintOptions =
        new() { MemberDisplayFormat = MemberDisplayFormat.SeparateLines };

    /// <summary>
    /// 已编译脚本类型的追踪列表。脚本执行时自动注册 typeof(Script)，
    /// 重新编译前用于清理旧脚本的事件订阅和静态字段。
    /// </summary>
    /// <remarks>
    /// P5 长期运行评估：Roslyn 脚本程序集加载进默认 AssemblyLoadContext，
    /// .NET（无 Thread.Abort / 不可卸载默认 ALC）下无法卸载，只能清理其静态字段与事件订阅
    /// （<see cref="CleanupOldScript"/>）。如需硬隔离（死循环、内存回收、安全），
    /// 应走独立 ScriptWorker 进程执行——<see cref="Contracts.IScriptRuntime"/>
    /// 契约已为此保留边界（Controls 只依赖契约，不依赖本静态类）。
    /// </remarks>
    public static readonly ConcurrentBag<Type> TrackedScriptTypes = new();

    /// <summary>调试模式下脚本源文件的临时目录。</summary>
    private static readonly string DebugSourceDirectory = Path.Combine(Path.GetTempPath(), "WpfScriptDebug");

    /// <summary>调试模式源文件路径缓存（key: 脚本源码哈希, value: 临时文件路径）。</summary>
    private static readonly ConcurrentDictionary<string, string> DebugSourceFiles = new();

    /// <summary>
    /// 静态构造：订阅程序集加载事件，捕获 Roslyn 编译生成的脚本动态程序集，
    /// 将其中的 Script 类型注册到 <see cref="TrackedScriptTypes"/>，
    /// 使重新编译前的 CleanupOldScript 能真正清理旧脚本的静态字段与事件订阅。
    /// （此前只有消费没有生产者，清理逻辑实际从未生效，多次执行后旧脚本
    /// 类型及其持有的引用持续累积，导致内存增长与 UI 卡顿。）
    /// </summary>
    static ScriptExecutionService()
    {
        AppDomain.CurrentDomain.AssemblyLoad += (_, args) =>
        {
            try
            {
                var assembly = args.LoadedAssembly;
                // 脚本提交程序集为动态生成（无物理位置）
                if (!assembly.IsDynamic) return;

                foreach (var type in assembly.GetTypes())
                {
                    // 脚本模板约定提交程序集中包含名为 Script 的用户类
                    //（普通 C# 类，并非继承 Microsoft.CodeAnalysis.Scripting.Script）
                    if (type.Name == "Script" && type.IsClass)
                        TrackedScriptTypes.Add(type);
                }
            }
            catch
            {
                // 单个动态程序集枚举失败时忽略，不影响宿主运行
            }
        };
    }

    // ════════════════════════════════════════════════════════════════
    //  Console 输出捕获（AsyncLocal 路由，多脚本输出互不干扰）
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 全局路由 Console 写入器，通过 AsyncLocal 将 Console 输出路由到当前执行的脚本的写入器。
    /// 初始化为一次，后续所有脚本共享此实例。
    /// 无脚本作用域时回退写入原始 Console.Out，保证宿主自身的 Console 输出不丢失。
    /// </summary>
    private sealed class RoutingConsoleWriter : TextWriter
    {
        private static readonly AsyncLocal<StreamingTextWriter?> _current = new();

        /// <summary>设置当前脚本上下文的 StreamingTextWriter。</summary>
        public static IDisposable EnterScope(StreamingTextWriter writer)
        {
            var old = _current.Value;
            _current.Value = writer;
            return new Scope(old);
        }

        /// <summary>
        /// Scope 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
        /// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
        /// </summary>
        private sealed class Scope(StreamingTextWriter? old) : IDisposable
        {
            /// <summary>
            /// 执行资源释放步骤；对应入口为 Dispose。
            /// </summary>
            public void Dispose() => _current.Value = old;
        }

        private static StreamingTextWriter? Current => _current.Value;

        /// <summary>当前脚本上下文存在则路由给脚本；否则回退到原始 Console.Out。</summary>
        private static TextWriter Target => (TextWriter?)Current ?? _originalOut;

        public override Encoding Encoding => Current?.Encoding ?? _originalOut?.Encoding ?? Encoding.UTF8;

        /// <summary>
        /// 执行写入操作；对应入口为 Write。
        /// </summary>
        public override void Write(char value) => Target?.Write(value);
        /// <summary>
        /// 执行写入操作；对应入口为 Write。
        /// </summary>
        public override void Write(string? value) => Target?.Write(value);
        /// <summary>
        /// 执行写入操作；对应入口为 WriteLine。
        /// </summary>
        public override void WriteLine(string? value) => Target?.WriteLine(value);
    }

    /// <summary>
    /// 原始 Console.Out（安装全局路由前捕获），作为无脚本作用域时的回退目标。
    /// </summary>
    private static TextWriter? _originalOut;

    /// <summary>防止并发首次执行时重复初始化 Console 路由。</summary>
    private static readonly object _consoleInitLock = new();

    /// <summary>
    /// 同时支持缓冲和实时流的 TextWriter。
    /// 每收到一行时调用 onOutput 回调，实现边执行边显示的效果。
    /// </summary>
    private sealed class StreamingTextWriter(Action<string>? onOutput) : TextWriter
    {
        private readonly StringBuilder _buffer = new();
        private readonly object _lock = new();

        public override Encoding Encoding => Encoding.UTF8;

        /// <summary>
        /// 执行写入操作；对应入口为 Write。
        /// 正文中的关键协作调用包括 Append；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public override void Write(char value)
        {
            lock (_lock) _buffer.Append(value);
        }

        /// <summary>
        /// 执行写入操作；对应入口为 Write。
        /// 正文中的关键协作调用包括 Append；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public override void Write(string? value)
        {
            if (value == null) return;
            lock (_lock) _buffer.Append(value);
        }

        /// <summary>
        /// 执行写入操作；对应入口为 WriteLine。
        /// 正文中的关键协作调用包括 Append；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public override void WriteLine(string? value)
        {
            var line = value + Environment.NewLine;
            lock (_lock)
            {
                _buffer.Append(line);
                onOutput?.Invoke(line);
            }
        }

        /// <summary>返回缓冲区的完整内容。</summary>
        public string GetBuffer() { lock (_lock) { return _buffer.ToString(); } }
    }

    // ════════════════════════════════════════════════════════════════
    //  公共方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建全新的脚本对象（每次独立编译，不使用 ContinueWith）。
    /// </summary>
    /// <param name="code">C# 脚本源代码。</param>
    /// <param name="host">Roslyn 宿主实例。</param>
    /// <param name="getAdditionalReferences">获取额外元数据引用的委托。</param>
    /// <param name="enableDebug">是否启用调试模式（生成 PDB + 源文件映射）。</param>
    /// <param name="globalsType">globals 的静态类型（用于让脚本源码能引用 globals 变量）。</param>
    /// <param name="implicitUsings">本次编译的隐式 using；null 时用进程默认选项（P5 任务隔离入口）。</param>
    /// <returns>编译就绪的脚本对象。</returns>
    public static Script<object> BuildScript(string code,
        RoslynHost host, Func<IReadOnlyList<MetadataReference>>? getAdditionalReferences,
        bool enableDebug = false, Type? globalsType = null, IEnumerable<string>? implicitUsings = null)
    {
        // 合并默认引用、框架程序集 + 宿主程序目录引用、用户额外引用
        IEnumerable<MetadataReference> allReferences = host.DefaultReferences;
        if (host is CustomRoslynHost)
            allReferences = allReferences.Concat(CustomRoslynHost.GetAdditionalDefaultReferences());
        if (getAdditionalReferences != null)
        {
            var additional = getAdditionalReferences();
            if (additional.Count > 0)
                allReferences = allReferences.Concat(additional);
        }

        var options = ScriptOptions.Default
            .AddReferences(allReferences)
            // 默认导入 + 隐式 using（任务级传入优先，否则进程默认；注册先后与编译顺序无关）
            .WithImports(host.DefaultImports.Concat(
                implicitUsings ?? ScriptRuntimeOptions.Default.Snapshot()).Distinct());

        if (enableDebug)
        {
            // 将源码写入临时 .cs 文件，使 VS 调试器可以找到源文件进行断点调试
            options = options.WithEmitDebugInformation(true)
                             .WithFilePath(PrepareDebugSource(code))
                             .WithFileEncoding(Encoding.UTF8);
        }

        return CSharpScript.Create(code, options, globalsType: globalsType);
    }

    /// <summary>
    /// 准备调试模式源文件：将脚本源码写入临时文件，使 VS 调试器可以关联源码。
    /// 如果文件被其他进程锁定，自动重试并回退到唯一文件名。
    /// </summary>
    /// <param name="code">脚本源代码。</param>
    /// <returns>临时文件的完整路径。</returns>
    public static string PrepareDebugSource(string code)
    {
        // 使用源码哈希作为缓存键，避免重复写入相同内容
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(code)));

        if (DebugSourceFiles.TryGetValue(hash, out var cachedPath) && File.Exists(cachedPath))
            return cachedPath;

        Directory.CreateDirectory(DebugSourceDirectory);
        var fileName = $"script_{hash[..12]}.cs";
        var filePath = Path.Combine(DebugSourceDirectory, fileName);

        // 写入临时文件，遇到文件锁时自动重试（最多 3 次，间隔 100ms）
        var written = TryWriteFileWithRetry(filePath, code);
        if (!written)
        {
            // 回退：使用唯一文件名避免冲突
            var fallbackFile = $"script_{hash[..12]}_{Guid.NewGuid():N}.cs";
            filePath = Path.Combine(DebugSourceDirectory, fallbackFile);
            File.WriteAllText(filePath, code, Encoding.UTF8);
        }

        DebugSourceFiles[hash] = filePath;
        return filePath;
    }

    /// <summary>尝试写入文件，遇到文件锁时自动重试。</summary>
    private static bool TryWriteFileWithRetry(string filePath, string code, int maxRetries = 3)
    {
        for (int i = 0; i <= maxRetries; i++)
        {
            try
            {
                File.WriteAllText(filePath, code, Encoding.UTF8);
                return true;
            }
            catch (IOException) when (i < maxRetries)
            {
                // 文件被锁定，等待后重试
                Thread.Sleep(100);
            }
        }
        return false;
    }

    /// <summary>
    /// 编译脚本并检测是否产生返回值。
    /// BuildScript 内部保证返回非 null 的 Script 对象。
    /// </summary>
    /// <param name="enableDebug">是否启用调试模式。</param>
    /// <returns>编译后的脚本对象和是否产生返回值的标志。</returns>
    public static (Script<object> Script, bool HasResult) CompileAndCheck(
        string code, RoslynHost host,
        Func<IReadOnlyList<MetadataReference>>? getAdditionalReferences,
        bool enableDebug = false, IEnumerable<string>? implicitUsings = null)
    {
        var script = BuildScript(code, host, getAdditionalReferences, enableDebug,
            globalsType: null, implicitUsings: implicitUsings);
        var compilation = script.GetCompilation();
        bool hasResult = HasSubmissionResultMethod.Invoke(compilation, null) as bool? == true;

        return (script, hasResult);
    }

    /// <summary>
    /// 异步编译脚本并检测是否产生返回值。
    /// 编译操作（BuildScript + GetCompilation + Compile）在线程池上执行，
    /// <param name="enableDebug">是否启用调试模式。</param>
    /// <param name="globalsType">globals 的静态类型（用于让脚本源码能引用 globals 变量）。</param>
    /// <returns>编译后的脚本对象、是否产生返回值的标志、诊断信息数组。</returns>
    public static async Task<(Script<object> Script, bool HasResult, Diagnostic[] Diagnostics)> CompileAndCheckAsync(
        string code, RoslynHost host,
        Func<IReadOnlyList<MetadataReference>>? getAdditionalReferences,
        bool enableDebug = false, Type? globalsType = null, IEnumerable<string>? implicitUsings = null)
    {
        return await Task.Run(() =>
        {
            var script = BuildScript(code, host, getAdditionalReferences, enableDebug, globalsType, implicitUsings);
            var compilation = script.GetCompilation();
            bool hasResult = HasSubmissionResultMethod.Invoke(compilation, null) as bool? == true;
            var diags = script.Compile().ToArray();
            return (script, hasResult, diags);
        });
    }

    /// <summary>
    /// 异步执行已编译的脚本。
    /// 捕获 Console 输出（通过重定向 Console.Out）和脚本返回值。
    /// </summary>
    /// <param name="script">已编译的脚本对象。</param>
    /// <param name="hasResult">脚本是否产生返回值。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="onOutput">实时输出回调（每收到一行时触发，从后台线程调用）。</param>
    /// <returns>格式化后的输出文本和是否发生错误的标志。</returns>
    public static async Task<(string Result, bool HasError, object? ReturnValue)> RunScriptAsync(
        Script<object> script, bool hasResult, CancellationToken cancellationToken = default,
        Action<string>? onOutput = null, object? globals = null)
    {
        // 首次调用时，将 Console.Out 替换为全局路由写入器（只做一次）。
        // 捕获原始 Console.Out 作为回退目标：脚本作用域外的输出（宿主自身日志等）
        // 仍写往原始控制台，不再被静默丢弃。
        if (_originalOut == null)
        {
            lock (_consoleInitLock)
            {
                if (_originalOut == null)
                {
                    _originalOut = Console.Out;
                    Console.SetOut(new RoutingConsoleWriter());
                }
            }
        }

        using var writer = new StreamingTextWriter(onOutput);
        using var scope = RoutingConsoleWriter.EnterScope(writer);

        try
        {
            // script.RunAsync() 并非真正异步（同步执行 + Task.FromResult 包装），
            // 直接在 UI 线程上调用会阻塞 UI。通过 Task.Run 强制放到线程池线程执行，
            // 同时降低该线程优先级，使 UI 线程优先获得 CPU 时间片。
            var result = await Task.Run(() =>
            {
                var origPriority = Thread.CurrentThread.Priority;
                Thread.CurrentThread.Priority = ThreadPriority.Lowest;
                try
                {
                    return script.RunAsync(globals: globals, cancellationToken: cancellationToken).GetAwaiter().GetResult();
                }
                finally
                {
                    try { Thread.CurrentThread.Priority = origPriority; } catch { }
                }
            });

            var consoleOutput = writer.GetBuffer();

            var output = new StringBuilder();

            if (!string.IsNullOrEmpty(consoleOutput))
            {
                output.AppendLine("=== Console 输出 ===");
                output.Append(consoleOutput);
            }
 
            bool hasError = false;
            object? returnValue = null;
            if (result.Exception != null)
            {
                hasError = true;
                output.AppendLine("=== 异常 ===");
                output.Append(FormatException(result.Exception));
            }
            else if (result.ReturnValue != null)
            {
                returnValue = result.ReturnValue;
                output.AppendLine("=== 返回值 ===");
                output.Append(FormatObject(result.ReturnValue));
            }

            // 如果没有任何输出，给出提示
            if (output.Length == 0)
            {
                output.AppendLine("脚本执行完毕，无输出。");
                output.AppendLine("提示：请使用 Console.WriteLine() 输出信息，或确保脚本方法有返回值。");
            }

            return (output.ToString(), hasError, returnValue);
        }
        catch (OperationCanceledException)
        {
            return ("执行已取消", false, null);
        }
        catch (Exception ex)
        {
            return (FormatException(ex), true, null);
        }
    }

    /// <summary>
    /// 格式化异常为可读字符串（极速版本）。
    /// 仅使用 ex.GetType() 和 ex.Message，不访问 ex.StackTrace，
    /// 因为 .NET 9 的 StackTrace 属性在首次访问时需 lazy 格式化成帧解析，
    /// N 个脚本并行报错时，每个首次访问需 35~265ms，累积耗时影响 UI 响应。
    /// </summary>
    public static string FormatException(Exception ex)
    {
        return $"{ex.GetType().FullName}: {ex.Message}";
    }

    /// <summary>格式化对象为可读字符串（分行显示成员）。</summary>
    /// <param name="o">要格式化的对象。</param>
    /// <returns>格式化后的对象字符串。</returns>
    public static string FormatObject(object o)
    {
        return CSharpObjectFormatter.Instance.FormatObject(o, PrintOptions);
    }

    /// <summary>
    /// 确保脚本代码末尾包含 Run 调用。
    /// 自动识别 Run 方法是 static 还是实例方法：
    /// static 形式追加 Script.Run()，实例形式追加 new Script().Run()（Initialize 与 Run 共享同一实例）。
    /// 如果有 Initialize/Load 方法，会在 Run 之前调用（无需 static）。
    /// 同时自动添加 #nullable enable 以消除可空注解警告。
    /// </summary>
    /// <param name="code">原始脚本代码。</param>
    /// <returns>处理后的脚本代码。</returns>
    public static string EnsureScriptRunInvoked(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return code;

        var trimmed = code.TrimEnd();

        // 如果脚本使用了 ? 可空注解但没开启 nullable 上下文，自动添加
        if (code.Contains('?') && !code.Contains("#nullable"))
        {
            code = "#nullable enable\r\n" + code;
            trimmed = code.TrimEnd();
        }

        // 已包含 Run 调用（静态或实例形式），无需修改
        string[] invokedEndings =
        {
            "Script.Run()", "await Script.Run()",
            "new Script().Run()", "await new Script().Run()",
            "__scriptInstance.Run()", "await __scriptInstance.Run()",
        };
        foreach (var ending in invokedEndings)
        {
            if (trimmed.EndsWith(ending) || trimmed.EndsWith(ending + ";"))
                return code;
        }

        // 不包含 Script 类，可能是纯表达式，不需要追加
        if (!code.Contains("class Script"))
            return code;

        // 查找 Run 声明行，判定 static / 实例形式（未找到时保持旧的静态形式，向后兼容）
        string? runDecl = FindMethodDeclLine(code, "Run");
        bool isStatic = runDecl == null || runDecl.Contains("static");

        // 注入 Inputs 字段到 Script 类：Roslyn 的 globals 字段在 type declarations 内不可见，
        // 重写 class Script 头，向其内部注入 dynamic Inputs 字段，让 Run() 内能直接写 Inputs.A。
        // 仅处理最常见的 `class Script {` 与 `class Script: ... {` 形式（保留可能的基类）。
        var injectField = isStatic
            ? "public static dynamic? Inputs; "
            : "public dynamic? Inputs; ";
        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"\bclass\s+Script\b(?:\s*:\s*[^{]*?)?\s*\{",
            m => m.Value + "\r\n#line hidden\r\n" + injectField + "\r\n#line default\r\n",
            System.Text.RegularExpressions.RegexOptions.None,
            System.TimeSpan.FromSeconds(1));

        // 判断是否包含 Initialize/Load 方法（static 或实例均可）
        bool hasInit = FindMethodDeclLine(code, "Initialize") != null
                    || FindMethodDeclLine(code, "Load") != null;

        // 判断是否为异步 Run 方法
        bool isAsyncRun = runDecl != null && runDecl.Contains("async");

        var suffix = new StringBuilder();

        if (isStatic)
        {
            // 顶层代码中 Inputs 解析到 globals（ScriptGlobals.Inputs），赋给静态字段后类内方法即可访问。
            suffix.Append("\r\nScript.Inputs = Inputs;");
            if (hasInit)
            {
                suffix.Append("\r\nScript.Initialize();");
            }
            suffix.Append(isAsyncRun ? "\r\nawait Script.Run()" : "\r\nScript.Run()");
        }
        else
        {
            // 实例形式：同一实例先 Initialize 再 Run，保证实例字段在两者之间共享
            const string instance = "__scriptInstance";
            // 顶层代码中 Inputs 解析到 globals（ScriptGlobals.Inputs），赋给实例字段后类内方法即可访问。
            suffix.Append($"\r\nvar {instance} = new Script();");
            suffix.Append($"\r\n{instance}.Inputs = Inputs;");
            if (hasInit)
            {
                suffix.Append($"\r\n{instance}.Initialize();");
            }
            suffix.Append(isAsyncRun ? $"\r\nawait {instance}.Run()" : $"\r\n{instance}.Run()");
        }

        return code + suffix.ToString();
    }

    /// <summary>
    /// 在脚本代码中查找指定名称的方法声明行（跳过注释、指令与调用语句）。
    /// 要求方法名前是空白符（返回类型与方法名之间必有空格），避免误命中 MyRun 之类的名称。
    /// </summary>
    /// <param name="code">脚本源代码。</param>
    /// <param name="methodName">方法名。</param>
    /// <returns>声明行 Trim 后的文本；未找到返回 null。</returns>
    private static string? FindMethodDeclLine(string code, string methodName)
    {
        foreach (var rawLine in code.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith("//") || line.StartsWith("*") || line.StartsWith("#"))
                continue;

            // 声明形式：返回类型 空格 方法名( ；调用形式（如 Script.Run()）前面是 . 或 ( ，不会命中
            int idx = Math.Max(line.IndexOf(" " + methodName + "(", StringComparison.Ordinal),
                               line.IndexOf("\t" + methodName + "(", StringComparison.Ordinal));
            if (idx < 0) continue;

            // 排除赋值/传参场景：= 或 , 之后出现的方法名
            string head = line.Substring(0, idx);
            if (head.Contains('=') || head.TrimEnd().EndsWith(","))
                continue;

            return line;
        }
        return null;
    }

    /// <summary>
    /// 在断点行插入调试器中断调用，实现源码级断点。
    /// 对每个断点行的可执行语句前插入 Debugger.Break() 调用，
    /// 使脚本执行到该行时跳入 VS 调试器。
    /// </summary>
    /// <param name="code">原始脚本源码。</param>
    /// <param name="breakpoints">断点行号集合（1-based）。</param>
    /// <returns>插桩后的源码。</returns>
    public static string InstrumentBreakpoints(string code, IEnumerable<int> breakpoints)
    {
        var bpSet = new HashSet<int>(breakpoints);
        if (bpSet.Count == 0) return code;

        // 统一行尾为 \r\n（Windows 标准），避免行尾混用导致 VS 提示"不一致的结尾"
        var normalized = code.Replace("\r\n", "\n").Replace("\r", "");
        var lines = normalized.Split('\n');
        var result = new StringBuilder(normalized.Length + bpSet.Count * 60);

        // 无法插桩的关键字行首（这些行不能直接后面加语句）
        static bool IsNonInstrumentableStart(string trimmed)
        {
            if (string.IsNullOrWhiteSpace(trimmed)) return true;
            var first = trimmed.AsSpan();
            if (first.StartsWith("namespace".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("class ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("struct ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("interface ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("public ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("private ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("protected ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("internal ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("using ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("//".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("#".AsSpan(), StringComparison.Ordinal))
                return true;

            // 续行检测：以下开头的行通常是多行语句的续行，不可插桩
            if (first.StartsWith(",".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith(".".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("?".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("=>".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith(")".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("]".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("}".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("+".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("-".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("*".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("/".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("%".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("&".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("|".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("^".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("<".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith(">".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("=".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("!".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("??".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("&&".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("||".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith(":".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith(";".AsSpan(), StringComparison.Ordinal))
                return true;

            return false;
        }

        // 控制流关键字行首：在这些行上插入 Debugger.Break() 需要使用 #line 前置插桩
        static bool IsControlFlowStart(string trimmed)
        {
            if (string.IsNullOrWhiteSpace(trimmed)) return false;
            var first = trimmed.AsSpan();
            return first.StartsWith("if ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("else".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("for ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("while ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("foreach ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("switch ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("try ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("catch ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("finally".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("do ".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("{".AsSpan(), StringComparison.Ordinal)
                || first.StartsWith("}".AsSpan(), StringComparison.Ordinal);
        }

        for (int i = 0; i < lines.Length; i++)
        {
            int lineNum = i + 1; // 1-based
            string line = lines[i];
            var trimmed = line.Trim();

            if (bpSet.Contains(lineNum) && IsControlFlowStart(trimmed))
            {
                // 控制流语句：用 #line 指令在行前插桩，使 VS 断点停在正确行号
                result.Append($"#line {lineNum}\r\n");
                result.Append($"    System.Diagnostics.Debugger.Break();\r\n");
                result.Append("#line default\r\n");
                result.Append(line);
                result.Append("\r\n");
            }
            else if (bpSet.Contains(lineNum) && !IsNonInstrumentableStart(trimmed))
            {
                // 普通可执行行：在行前插入 Debugger.Break()，并用 #line 保持行号映射
                result.Append($"#line {lineNum}\r\n");
                result.Append($"    System.Diagnostics.Debugger.Break();\r\n");
                result.Append("#line default\r\n");
                result.Append(line);
                result.Append("\r\n");
            }
            else
            {
                // 不可插桩行或无断点：原样输出
                result.Append(line);
                result.Append("\r\n");
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 在重新编译前自动清理所有旧脚本的事件订阅和静态字段。
    /// 从 TrackedScriptTypes 收集已知的旧脚本类型进行清理。
    /// </summary>
    public static void CleanupOldScript()
    {
        var scriptTypes = new HashSet<Type>();

        // 从追踪列表中收集（脚本执行时自动注册）
        while (TrackedScriptTypes.TryTake(out var type))
        {
            scriptTypes.Add(type);
        }

        // 如果没有追踪的脚本类型，无需扫描
        // Roslyn 编译的脚本程序集难以通过名称可靠识别，
        // 全程序集反射扫描代价过高（AppDomain.GetAssemblies + GetTypes），
        // 故不在此处兜底扫描。如需清理，请在脚本执行时注册到 TrackedScriptTypes。

        if (scriptTypes.Count == 0) return;

        System.Diagnostics.Debug.WriteLine($"[CleanupOldScript] 清理 {scriptTypes.Count} 个旧的 Script 类型");

        foreach (var type in scriptTypes)
        {
            CleanupScriptType(type);
        }
    }

    /// <summary>
    /// 清理单个 Script 类型：清空其引用的服务实例的所有事件，然后清空所有静态字段。
    /// </summary>
    private static void CleanupScriptType(Type type)
    {
        try
        {
            var flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            var fields = type.GetFields(flags);
            if (fields.Length == 0) return;

            // 收集所有引用类型字段实例（服务对象、handler 委托等）
            foreach (var field in fields)
            {
                var value = field.GetValue(null);
                if (value == null) continue;

                // 如果是引用类型实例，清空其所有事件的底层 delegate
                if (!field.FieldType.IsValueType && field.FieldType != typeof(string))
                {
                    ClearAllDelegateFields(value);
                }
            }

            // 清空 Script 类的所有静态字段
            foreach (var field in fields)
            {
                try
                {
                    if (field.FieldType.IsValueType && !field.FieldType.IsEnum)
                        field.SetValue(null, Activator.CreateInstance(field.FieldType));
                    else
                        field.SetValue(null, null);
                }
                catch { }
            }

            System.Diagnostics.Debug.WriteLine($"[CleanupScriptType] 已清理: {type.FullName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CleanupScriptType] 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 递归清空对象所有私有 Delegate 类型字段（即事件底层委托）。
    /// 不依赖字段名匹配，直接遍历所有 Delegate 字段并设为 null。
    /// </summary>
    private static void ClearAllDelegateFields(object instance)
    {
        if (instance == null) return;

        try
        {
            var type = instance.GetType();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            foreach (var field in type.GetFields(flags))
            {
                if (!typeof(Delegate).IsAssignableFrom(field.FieldType))
                    continue;

                try
                {
                    var oldValue = field.GetValue(instance);
                    if (oldValue != null)
                    {
                        field.SetValue(instance, null);
                        System.Diagnostics.Debug.WriteLine($"[ClearAllDelegateFields] 解绑事件: {type.Name}.{field.Name}");
                    }
                }
                catch { }
            }
        }
        catch { }
    }
}
