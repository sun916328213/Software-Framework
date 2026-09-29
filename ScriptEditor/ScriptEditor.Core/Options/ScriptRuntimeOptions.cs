using System.Text.RegularExpressions;

namespace ScriptEditor.Options;

/// <summary>
/// 脚本运行时选项：隐式 using（global imports）的显式持有者。
/// </summary>
/// <remarks>
/// P5 产物：替代原来散落在 <c>CustomRoslynHost</c> 进程静态集合中的
/// <c>GlobalImports</c>。隐式 using 的注册/读取收敛到实例上：
/// <list type="bullet">
///   <item><see cref="Default"/>：进程默认选项（编辑器文档会话、无头单脚本场景沿用此前行为）；</item>
///   <item><see cref="ScriptProjectContext"/>：每个任务一份，任务之间不再互相污染。</item>
/// </list>
/// Core 层拥有本类型（含 <see cref="ScriptProjectContext"/>），Runtime/Controls 只消费，
/// 因此上下文可在契约（<c>IScriptSession</c>）中出现而不破坏分层。
/// </remarks>
public sealed class ScriptRuntimeOptions
{
    /// <summary>进程默认选项（兼容旧静态 GlobalImports 的语义：显式的单一写入点）。</summary>
    public static ScriptRuntimeOptions Default { get; } = new();

    private readonly HashSet<string> _implicitUsings = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>当前隐式 using 快照。</summary>
    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate) return _implicitUsings.ToArray();
    }

    /// <summary>注册单个隐式 using（幂等，空值忽略）。</summary>
    public void Add(string? ns)
    {
        var trimmed = ns?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;
        lock (_gate) _implicitUsings.Add(trimmed);
    }

    /// <summary>批量注册隐式 using（幂等）。</summary>
    public void AddRange(IEnumerable<string>? namespaces)
    {
        if (namespaces == null) return;
        lock (_gate)
        {
            foreach (var ns in namespaces)
            {
                var trimmed = ns?.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    _implicitUsings.Add(trimmed);
            }
        }
    }

    /// <summary>从脚本源码提取普通 using 指令并注册（自动排除 using static 与别名形式）。</summary>
    public void AddFromSource(string? sourceCode)
    {
        if (string.IsNullOrEmpty(sourceCode)) return;
        AddRange(UsingDirectives.ExtractNamespaces(sourceCode));
    }

    /// <summary>清空全部隐式 using（主要用于测试隔离）。</summary>
    public void Clear()
    {
        lock (_gate) _implicitUsings.Clear();
    }
}

/// <summary>using 指令提取（纯函数）。</summary>
public static class UsingDirectives
{
    private static readonly Regex UsingDirectivePattern = new(
        @"^\s*using\s+(?!static\b)([A-Za-z_][A-Za-z0-9_.]*)\s*;\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>提取源码中的普通 using 命名空间（去重保序）。</summary>
    public static IReadOnlyList<string> ExtractNamespaces(string sourceCode)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(sourceCode)) return result;
        foreach (Match match in UsingDirectivePattern.Matches(sourceCode))
        {
            var ns = match.Groups[1].Value;
            if (!result.Contains(ns))
                result.Add(ns);
        }
        return result;
    }
}

/// <summary>
/// 脚本工程上下文：一个任务的运行期配置（隐式 using + 引用基准目录）。
/// </summary>
/// <remarks>
/// P5 产物：原来任务加载/保存/回填时调用进程静态
/// <c>AddGlobalImportsFromSource</c>（任务之间互相污染、长期运行无法回收），
/// 现收敛为每个任务一份上下文，会话编译时只读自己任务的集合。
/// </remarks>
public sealed class ScriptProjectContext
{
    /// <summary>任务名（诊断用）。</summary>
    public string Name { get; set; } = "新建任务";

    /// <summary>工程目录（引用相对路径解析基准；未知时为 null）。</summary>
    public string? BaseDirectory { get; set; }

    /// <summary>本任务独占的隐式 using 集合（不与其它任务共享）。</summary>
    public ScriptRuntimeOptions Usings { get; } = new();

    /// <summary>从一批脚本源码收敛隐式 using（本任务内幂等）。</summary>
    public void AddSources(IEnumerable<string?> codes)
    {
        foreach (var code in codes)
            Usings.AddFromSource(code);
    }

    /// <summary>
    /// 解析本次编译应使用的隐式 using：本任务集合非空时用本任务的，
    /// 否则回退到进程默认（与 P5 前单任务行为一致）。
    /// </summary>
    public IReadOnlyList<string> ResolveUsings()
    {
        var own = Usings.Snapshot();
        return own.Count > 0 ? own : ScriptRuntimeOptions.Default.Snapshot();
    }
}
