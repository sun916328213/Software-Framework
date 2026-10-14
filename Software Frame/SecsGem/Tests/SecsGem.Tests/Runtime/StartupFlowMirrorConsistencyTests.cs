// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：StartupFlowMirrorConsistencyTests —— 钉住 taskproj 嵌入脚本与镜像 .cs 不再漂移。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace SecsGem.Tests.Runtime;

/// <summary>
/// 启动流程「嵌入代码 vs 镜像 .cs」的一致性守卫。
/// <para>
/// 背景：SecsGem 重构删除了 <c>SecsControl</c>，镜像 .cs 全部迁移到 <c>ISecsGemRuntime</c>，
/// 但 taskproj 里有三个 script 的嵌入代码仍引用 <c>SecsControl</c>（全仓已无该类型）⇒
/// 启动流程「初始化基础控件 / 初始化工具 / 初始化后台」三步在编译期就会失败。
/// </para>
/// <para>
/// 既有守卫 <c>ProductionDeviceSessionTests.PersistedStartupFlowUsesTheVerifiedNewDeviceScript</c>
/// 只覆盖 Scripts[2]（初始化硬件）——而那恰好是唯一未漂移的一条。有守卫的不漂移、
/// 没守卫的三条全漂移，说明缺守卫就是根因。本测试把覆盖扩到全部四条，且不依赖额外
/// 中间文件：直接比对 taskproj 嵌入代码与同名镜像 .cs。
/// </para>
/// </summary>
public sealed class StartupFlowMirrorConsistencyTests
{
    /// <summary>taskproj 的 Scripts[] 下标 → 镜像文件名通配符。</summary>
    private static readonly IReadOnlyDictionary<int, string> Mirrors = new Dictionary<int, string>
    {
        [0] = "01_*.cs",
        [1] = "02_*.cs",
        [2] = "04_*.cs",
        [3] = "05_*.cs",
    };

    private static string Root()
    {
        // 测试输出在 <repo>/Software Frame/SecsGem/Tests/SecsGem.Tests/bin/<cfg>/<tfm>/
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Software Frame.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir.FullName;
    }

    private static string Normalize(string text)
    {
        // 只比代码骨架：去掉行注释、块注释与全部空白差异。
        text = Regex.Replace(text, @"(?m)//.*$", string.Empty);
        text = Regex.Replace(text, @"(?s)/\*.*?\*/", string.Empty);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string EmbeddedCode(JsonElement scripts, int index) =>
        scripts[index].GetProperty("Code").GetString()!;

    /// <summary>
    /// 全部四个 script 的嵌入代码必须与镜像 .cs 骨架一致。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EmbeddedScriptMatchesItsMirror(int index)
    {
        var root = Root();
        var flowDir = Path.Combine(root, "MachineApp", "MachineApp", "Config", "Flow", "StartupFlow");
        using var project = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(flowDir, "StartupFlow.taskproj")));
        var scripts = project.RootElement.GetProperty("Scripts");

        var mirrorPath = Directory.GetFiles(flowDir, Mirrors[index]).Single();
        var mirror = File.ReadAllText(mirrorPath).TrimStart('﻿');

        Assert.Equal(Normalize(mirror), Normalize(EmbeddedCode(scripts, index)));
    }

    /// <summary>
    /// 启动流程不得再引用已随 SecsGem 重构删除的 <c>SecsControl</c>。
    /// </summary>
    [Fact]
    public void NoStartupScriptReferencesRemovedSecsControl()
    {
        var root = Root();
        var taskproj = Path.Combine(root, "MachineApp", "MachineApp", "Config", "Flow", "StartupFlow", "StartupFlow.taskproj");
        using var project = JsonDocument.Parse(File.ReadAllText(taskproj));

        foreach (var script in project.RootElement.GetProperty("Scripts").EnumerateArray())
        {
            var title = script.GetProperty("Title").GetString();
            var code = script.GetProperty("Code").GetString()!;
            Assert.False(Regex.IsMatch(code, @"\bSecsControl\b"),
                $"启动脚本「{title}」仍引用 SecsControl —— 该类型已随 SecsGem 重构删除，全仓无定义。");
            Assert.False(Regex.IsMatch(code, @"_ConnectManager|_StatusManager"),
                $"启动脚本「{title}」仍引用 SecsControl 的旧内部成员 —— 应改用 ISecsGemRuntime.GetState()。");
        }
    }

    /// <summary>
    /// 全仓不得存在 <c>SecsControl</c> 定义 —— 否则上面两条会变成空断言。
    /// </summary>
    [Fact]
    public void SecsControlIsActuallyGoneFromTheRepository()
    {
        var root = Root();
        var offenders = new List<string>();
        foreach (var dir in new[] { "MachineApp", "Software Frame" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, dir), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(@"\bin\") || file.Contains(@"\obj\")) continue;
                if (Regex.IsMatch(File.ReadAllText(file), @"\bclass\s+SecsControl\b"))
                    offenders.Add(Path.GetRelativePath(root, file));
            }
        }
        Assert.Empty(offenders);
    }

    /// <summary>
    /// 三个已迁移的 script 所引用的 SECS 类型必须能真正加载 —— 这是 M01 修复的核心效果。
    /// <para>
    /// 前两条只证明「不再引用已删除类型」，不证明「引用到的类型存在」。启动脚本由 FlowEditor
    /// 在运行时经 Roslyn 编译，其宿主不引用 ScriptEditor.Runtime，因此这里不加新依赖，
    /// 直接对已编译程序集做反射解析。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("SecsGem.Core.ISecsGemRuntime", "SecsGem.Core")]
    public void SecsGemTypesReferencedByStartupScriptsResolve(string typeName, string assemblyName)
    {
        var type = Assembly.Load(assemblyName).GetType(typeName, throwOnError: false);
        Assert.True(type != null, $"{assemblyName} 中找不到 {typeName}");
        Assert.True(type!.IsInterface, $"{typeName} 应是接口（SecsGem.Core 的业务门面契约）");

        // 脚本实际调用的成员也必须存在，否则运行到该行才炸
        Assert.NotNull(type.GetMethod("GetState"));
        Assert.NotNull(type.GetMethod("ConnectAsync"));
        Assert.NotNull(type.GetMethod("DisconnectAsync"));
        Assert.NotNull(type.GetMethod("GoOnlineLocalAsync"));
        Assert.NotNull(type.GetMethod("GoOnlineRemoteAsync"));
        Assert.NotNull(type.GetMethod("GoOfflineAsync"));
    }

    /// <summary>
    /// <c>SecsGemState</c> 上被后台脚本使用的成员（Mode / IsConnected）必须存在。
    /// </summary>
    [Fact]
    public void SecsGemStateExposesTheMembersStartupScriptsUse()
    {
        var core = Assembly.Load("SecsGem.Core");
        var state = core.GetType("SecsGem.Core.SecsGemState", throwOnError: true)!;
        var mode = core.GetType("SecsGem.Core.SecsGemMode", throwOnError: true)!;

        Assert.NotNull(state.GetProperty("Mode"));
        Assert.NotNull(state.GetProperty("IsConnected"));
        Assert.Equal(mode, state.GetProperty("Mode")!.PropertyType);

        // 后台脚本的 switch 覆盖 OnlineLocal / OnlineRemote 两个分支
        Assert.NotNull(mode.GetField("OnlineLocal"));
        Assert.NotNull(mode.GetField("OnlineRemote"));
    }

    /// <summary>
    /// 行注释不得吞掉同一行后面的<em>花括号</em>（<c>if (...)//说明        {</c> 这类）。
    /// <para>
    /// 补这个洞的由来：<c>05_初始化后台.cs</c> 曾写成
    /// <c>if (now.Minute % 2 == 0 &amp;&amp; now.Second == 0)//5分钟读一次        {</c>
    /// —— 行注释把 <c>{</c> 一起吞掉，<c>if</c> 退化成无花括号单语句，随后的 <c>}</c> 多余、
    /// <c>else</c> 报 CS1519。引入于 <c>355750a 软件优化</c>，且在运行路径上（镜像是唯一被编译的）。
    /// </para>
    /// <para>
    /// 为什么既有守卫漏了它：<c>EmbeddedScriptMatchesItsMirror</c> 只比对 taskproj 与镜像是否一致
    /// —— 两边<em>同样错</em>时它照样通过。
    /// </para>
    /// </summary>
    [Fact]
    public void NoMirrorCommentSwallowsFollowingCode()
    {
        var flowDir = Path.Combine(Root(), "MachineApp", "MachineApp", "Config", "Flow", "StartupFlow");
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(flowDir, "*.cs"))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var comment = lines[i].IndexOf("//", StringComparison.Ordinal);
                if (comment < 0) continue;
                var before = lines[i][..comment].Trim();
                var tail = lines[i][(comment + 2)..].Trim();
                // 整行注释（// 前无代码）合法。
                // 行尾注释也合法 —— 只要注释之后没有留下**代码**。
                // 真正要抓的是「// 注释吞掉了本行后面的代码」，典型是
                //     if (cond)//说明        {
                // 这里 { 落在注释之后（可能隔着说明文字），于是 if 失去花括号块。
                // 判据：注释之后出现 { 或 } —— 这两个字符会改变语义，必须在前一行。
                if (before.Length > 0 && Regex.IsMatch(tail, @"[{}]"))
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}  {lines[i].Trim()}");
            }
        }
        Assert.True(offenders.Count == 0,
            "行注释吞掉了后续代码（会破坏编译）：" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// 四个镜像的花括号必须配平。
    /// <para>
    /// 用朴素的括号计数而非 Roslyn：本测试工程没有引用 Microsoft.CodeAnalysis，而为一次
    /// 语法检查新增包依赖不值得。计数时剔除行/块注释与字符串字面量，避免误报。
    /// 它能抓住的正是本轮真实遇到的那类错误（<c>{</c> 被注释吞掉导致 <c>}</c> 多余）。
    /// 不能替代真编译 —— 语义错误（缺成员、类型不匹配）不在其覆盖范围内。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("01_*.cs")]
    [InlineData("02_*.cs")]
    [InlineData("04_*.cs")]
    [InlineData("05_*.cs")]
    public void MirrorBracesAreBalanced(string pattern)
    {
        var flowDir = Path.Combine(Root(), "MachineApp", "MachineApp", "Config", "Flow", "StartupFlow");
        var path = Directory.GetFiles(flowDir, pattern).Single();

        var source = Regex.Replace(File.ReadAllText(path), @"(?s)/\*.*?\*/", " ");
        source = Regex.Replace(source, @"(?m)//.*$", " ");
        source = Regex.Replace(source, @"""(?:[^""\\]|\\.)*""", "\"\"");

        var open = source.Count(c => c == '{');
        var close = source.Count(c => c == '}');
        Assert.True(open == close, $"{Path.GetFileName(path)} 花括号不配平：{{ {open} 个 }} {close} 个");
    }

    // ─────────────────── A1：taskfiles.manifest.json 与 ScriptId ───────────────────
    // 为什么需要这一组：ScriptManagerService 加载 taskproj 时优先按清单的 ScriptId 匹配
    // （ImportScriptsFromFolder -> ImportByManifest 的 byId 字典）。
    // 磁盘上的 taskproj 一旦缺 ScriptId，ScriptItem 就会每次加载拿到新的随机 Guid，
    // 匹配全部落空 —— .cs 永远不会被导入，而且**没有任何报错**。
    // 缺清单则退回按文件时间戳判断，新克隆的仓库里 .cs 与 taskproj 时间戳任意，
    // 谁生效纯属偶然。这三条把那种偶然钉死成确定行为。

    private static string Sha256Short(string content, int length = 16)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
               System.Text.Encoding.UTF8.GetBytes(content ?? string.Empty)))[..length];

    private static JsonElement LoadManifest()
    {
        var path = Path.Combine(Root(), "MachineApp", "MachineApp", "Config", "Flow", "StartupFlow",
            "taskfiles.manifest.json");
        Assert.True(File.Exists(path), "缺少 taskfiles.manifest.json —— 加载会退回按时间戳判定，.cs 与 taskproj 谁生效变成偶然");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    private static JsonElement LoadTaskProject()
    {
        var path = Path.Combine(Root(), "MachineApp", "MachineApp", "Config", "Flow", "StartupFlow",
            "StartupFlow.taskproj");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    /// <summary>清单里的每条哈希必须等于对应磁盘 .cs 的哈希，否则 FolderHasNewerSources 判定失真。</summary>
    [Fact]
    public void ManifestHashesMatchTheCsFilesOnDisk()
    {
        var flowDir = Path.Combine(Root(), "MachineApp", "MachineApp", "Config", "Flow", "StartupFlow");
        var files = LoadManifest().GetProperty("Files");
        Assert.True(files.GetArrayLength() > 0, "清单为空");

        foreach (var entry in files.EnumerateArray())
        {
            var name = entry.GetProperty("FileName").GetString()!;
            var path = Path.Combine(flowDir, name);
            Assert.True(File.Exists(path), $"清单列出的 {name} 不存在");
            // 读法必须与 C# 侧 File.ReadAllText 一致（BOM 感知），否则哈希对不上
            var actual = Sha256Short(File.ReadAllText(path));
            var expected = entry.GetProperty("ContentHash").GetString()!;
            Assert.True(actual == expected, $"{name} 哈希与清单不符：磁盘={actual} 清单={expected}");
        }
    }

    /// <summary>taskproj 每条 script 必须带非空 ScriptId，且与清单一一对应。</summary>
    [Fact]
    public void TaskProjectScriptIdsMatchTheManifest()
    {
        var scripts = LoadTaskProject().GetProperty("Scripts");
        var entries = LoadManifest().GetProperty("Files").EnumerateArray().ToArray();
        Assert.Equal(scripts.GetArrayLength(), entries.Length);

        var idsInManifest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var id = entry.GetProperty("ScriptId").GetString();
            Assert.True(!string.IsNullOrWhiteSpace(id) && id != Guid.Empty.ToString(),
                "清单存在空 ScriptId —— ImportByManifest 会直接跳过该条");
            idsInManifest.Add(id!);
        }

        foreach (var script in scripts.EnumerateArray())
        {
            var title = script.GetProperty("Title").GetString();
            Assert.True(script.TryGetProperty("ScriptId", out var idProp), $"{title} 缺 ScriptId");
            var id = idProp.GetString();
            Assert.True(!string.IsNullOrWhiteSpace(id), $"{title} 的 ScriptId 为空");
            Assert.True(idsInManifest.Contains(id!),
                $"{title} 的 ScriptId={id} 在清单中找不到 —— 加载时 byId 匹配落空，.cs 永远不会被导入");
        }
    }

    /// <summary>目录里不得有清单未登记的 .cs，否则它会被静默忽略（或反过来触发全量导入）。</summary>
    [Fact]
    public void NoUnregisteredCsFilesInScriptFolder()
    {
        var flowDir = Path.Combine(Root(), "MachineApp", "MachineApp", "Config", "Flow", "StartupFlow");
        var known = LoadManifest().GetProperty("Files").EnumerateArray()
            .Select(e => e.GetProperty("FileName").GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 与 ScriptManagerService.ScriptFileRegex 同一条正则
        var orphans = Directory.GetFiles(flowDir, "*.cs")
            .Select(Path.GetFileName)
            .Where(n => n != null && Regex.IsMatch(n!, @"^\d{2}_.+\.cs$", RegexOptions.IgnoreCase))
            .Where(n => !known.Contains(n!))
            .ToArray();

        Assert.True(orphans.Length == 0, "清单未登记的脚本文件：" + string.Join(", ", orphans));
    }

    /// <summary>
    /// taskproj 的 Code 与磁盘 .cs 必须逐字节一致。
    /// 有清单且哈希一致时不会触发导入，实际编译的是 taskproj 里的副本 —— 两者必须真的相同，
    /// 否则「谁生效」这个问题只是从时间戳偶然换成了内容漂移。
    /// </summary>
    [Fact]
    public void TaskProjectCodeIsByteIdenticalToItsCsFile()
    {
        var flowDir = Path.Combine(Root(), "MachineApp", "MachineApp", "Config", "Flow", "StartupFlow");
        var scripts = LoadTaskProject().GetProperty("Scripts").EnumerateArray().ToArray();
        var files = Directory.GetFiles(flowDir, "*.cs")
            .Where(f => Regex.IsMatch(Path.GetFileName(f), @"^\d{2}_.+\.cs$", RegexOptions.IgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)   // 与 ImportScriptsFromFolder 的无清单分支同序
            .ToArray();
        Assert.Equal(files.Length, scripts.Length);

        for (int i = 0; i < files.Length; i++)
        {
            var embedded = scripts[i].GetProperty("Code").GetString()!;
            var onDisk = File.ReadAllText(files[i]);
            Assert.True(embedded == onDisk,
                $"{Path.GetFileName(files[i])}：taskproj 副本与磁盘不一致（长度 {embedded.Length} vs {onDisk.Length}）");
        }
    }
}