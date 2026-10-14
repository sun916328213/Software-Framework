// 【中文阅读说明】
// MachineApp 模块：设备/产线控制、集合、调度、配方、可视化与设备联动；主机资产与实际运行配置由部署包和工厂决定。
// 本文件定义：StartupFlowScriptCompilationTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Microsoft.CodeAnalysis、ScriptEditor.Runtime.Services、System、System.IO、System.Linq、System.Text.Json、System.Threading.Tasks、Xunit。导入不代表所有类型都会在运行时加载。
using Microsoft.CodeAnalysis;
using ScriptEditor.Runtime.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace MachineApp.Tests;

/// <summary>
/// 启动脚本的<strong>真实编译</strong>守卫。
///
/// <para>
/// 为什么必须真编译：本文件之前只有「taskproj 与镜像骨架一致」这类<em>文本</em>守卫，
/// 它挡不住「两边一样但都编译不过」。实测就漏过一次：<c>05_初始化后台.cs</c> 少 5 行 using
/// （<c>WPFControls.ProgramControl.Services</c>、<c>MachineApp.Infra.Core</c>、
/// <c>MachineApp.Data</c>、<c>WPFControls.ProgramControl.Models</c>、<c>Extension.IO.Retention</c>），
/// 5 个类型全部解析失败 —— 而同一文件夹的 <c>01_初始化控件.cs</c> 一直有其中两行。
/// </para>
///
/// <para>
/// 为什么走 <see cref="ScriptExecutionService.CompileAndCheckAsync"/> 而不是自己拼引用集：
/// 脚本是 <c>CSharpScript</c>（不是 <c>CSharpCompilation</c>），带隐式 using；
/// <c>globalsType</c> 是 <see cref="ScriptGlobals"/>；宿主还会用
/// <see cref="ScriptExecutionService.EnsureScriptRunInvoked"/> 往 <c>class Script</c> 里
/// 注入 <c>Inputs</c> 字段并追加 <c>Run()</c> 调用；引用集由
/// <c>RuntimeReferenceCache</c> 按「框架目录（WindowsDesktop 覆盖 NETCore）+ 宿主目录 DLL（跳过框架同名）」解析。
/// 任何一处自己复刻都会失真 —— 我用 <c>CSharpCompilation</c> 复刻过一次，得到的是
/// <c>CS0009 / CS1705</c> 这类<em>探针自己的</em>错误，不是脚本的问题。
/// </para>
///
/// <para>
/// 参数与生产路径等价，已逐项核对：<c>getReferences</c> 在生产上是
/// <c>ScriptItem.ScriptReferences</c>（本 taskproj 四条脚本的 <c>References</c> 全是 <c>[]</c>），
/// <c>implicitUsings</c> 在生产上来自 <c>ProjectContext</c>，而 <c>GetOrCreateSession</c> 并未设置它
/// ⇒ 落到 <c>ScriptRuntimeOptions.Default</c>，该集合<em>生产代码里没有任何注册者</em>，恒为空。
/// </para>
///
/// <para>
/// 启动时这四条脚本在 <c>App.xaml.cs</c> 里逐条 <c>try/catch</c> 执行，失败只把错误文本
/// 收进 <c>initErrors</c>，<strong>不会阻断应用启动</strong> —— 所以编译不过等于「那一步静默不执行」，
/// 本类就是防止它静默下去。
/// </para>
/// </summary>
public sealed class StartupFlowScriptCompilationTests
{
    /// <summary>启动脚本目录的绝对路径（测试输出在 MachineApp/Tests/.../bin/&lt;cfg&gt;/&lt;tfm&gt;/）。</summary>
    private static string StartupFlowDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Software Frame.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "MachineApp", "MachineApp", "Config", "Flow", "StartupFlow");
    }

    /// <summary>taskproj 里四条脚本的标题，按下标顺序。</summary>
    private static IReadOnlyList<(string Title, string Code)> ReadTaskProjectScripts()
    {
        var json = File.ReadAllText(Path.Combine(StartupFlowDirectory(), "StartupFlow.taskproj"));
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("Scripts").EnumerateArray()
            .Select(x => (x.GetProperty("Title").GetString() ?? "", x.GetProperty("Code").GetString() ?? ""))
            .ToArray();
    }

    /// <summary>走生产编译管线取错误诊断。只编译，不执行。</summary>
    private static async Task<Diagnostic[]> CompileAsync(string code)
    {
        // 生产路径先做后缀注入（ScriptSession.cs:408），漏掉会导致编译模型与线上不同
        var prepared = ScriptExecutionService.EnsureScriptRunInvoked(code);
        var (_, _, diagnostics) = await ScriptExecutionService.CompileAndCheckAsync(
            prepared,
            ScriptHostProvider.Shared,
            getAdditionalReferences: null,          // 见类注释：taskproj 的 References 全空
            enableDebug: false,
            globalsType: typeof(ScriptGlobals),
            implicitUsings: null);                 // 见类注释：生产上恒为空
        return diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    }

    /// <summary>
    /// 启动脚本<strong>不得再出现硬编码的产能数值</strong>。
    /// <para>
    /// 这条防的是一类特定回归：本仓库的看板脚本曾长期推一串写死的数字
    /// （OK=<c>{10}</c>、NG=<c>{20,20,30,40,130,20,0}</c>、Total=<c>{120,120,130,140,230,120,0}</c>），
    /// 看板上显示的一直是假数据。现已改为经 <c>CapacityStatisticsService</c> 读真实值。
    /// </para>
    /// <para>
    /// 为什么值得单独立一条：<b>编译守卫抓不到它</b>。写死的数字语法完全合法，
    /// <c>All_Four_Startup_Scripts_Compile</c> 照样全绿 —— 实测把 <c>ReadCount</c> 里的
    /// <c>publish(...)</c> 删掉，两个测试工程都是 0 红。
    /// </para>
    /// </summary>
    [Fact]
    public void StartupScripts_Contain_NoHardcodedCapacityNumbers()
    {
        var flowDir = StartupFlowDirectory();
        // 只盯产能/看板相关的序列构造，不泛化到所有数字字面量
        var suspicious = new[]
        {
            @"ObservableCollection<double>\s*\{\s*\d",
            @"Value\s*=\s*new\s+ObservableCollection<double>\s*\{\s*\d",
        };

        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(flowDir, "*.cs"))
            foreach (var line in File.ReadAllLines(file))
            {
                var trimmed = line.TrimStart();
                // 跳过注释行（说明文字里会引用这些历史数值）
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;
                if (suspicious.Any(p => Regex.IsMatch(line, p)))
                    offenders.Add($"{Path.GetFileName(file)}: {trimmed}");
            }

        Assert.True(offenders.Count == 0,
            "启动脚本仍在推送硬编码产能数值（看板会显示假数据）："
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// <c>ReadCount</c> 必须真的把查询结果推给看板。
    /// 上一条只能证明「没有硬编码数值」，证明不了「读了数据并且用上了」——
    /// 删掉 <c>publish(...)</c> 这一行，两个测试工程都不会红。
    /// </summary>
    [Fact]
    public void ReadCount_ActuallyPublishesTheQueriedTrend()
    {
        var path = Directory.GetFiles(StartupFlowDirectory(), "05_*.cs").Single();
        var code = File.ReadAllText(path);

        Assert.Contains("ReadCount", code, StringComparison.Ordinal);
        // 取数 → 发布 必须成对出现
        Assert.Contains("GetStatistics()", code, StringComparison.Ordinal);
        Assert.Contains("publish(", code, StringComparison.Ordinal);
        // 看板两个入口都要接上，不能只接一半
        Assert.Contains("ShowData_Day", code, StringComparison.Ordinal);
        Assert.Contains("ShowData_Month", code, StringComparison.Ordinal);
        // ReadCount 的实现里必须真的调用 publish，不能只是留个参数
        var body = code[(code.IndexOf("private void ReadCount(", StringComparison.Ordinal))..];
        Assert.Contains("publish(statistics.Model.OutputTrend", body, StringComparison.Ordinal);
    }
    /// <summary>
    /// PLC 状态不得再写死。
    /// <para>
    /// 原先是 <c>var plcstatus = ELinkStatus.Offline;</c> —— 常量使下面 <c>if (Online)</c> 分支
    /// <strong>永不执行</strong>，PLC 指示灯也永远不会亮。现改为读 <c>_PLCMethod.IsOnline</c>
    /// （与 <c>AutoProcess.cs:242</c> 同写法）。
    /// </para>
    /// <para>
    /// 同样地，编译守卫抓不到它 —— 硬编码常量语法完全合法。这条针对的是<strong>内容</strong>。
    /// </para>
    /// </summary>
    [Fact]
    public void PlcStatus_IsNotHardcodedOffline()
    {
        var path = Directory.GetFiles(StartupFlowDirectory(), "05_*.cs").Single();
        var code = string.Join(Environment.NewLine, File.ReadAllLines(path)
            .Where(l => !l.TrimStart().StartsWith("//")));   // 注释里会引用旧的写法

        var hardcoded = Regex.Matches(code, @"(?m)^\s*(var|ELinkStatus)\s+plcstatus\s*=\s*ELinkStatus\.Offline\s*;")
            .Cast<Match>().Select(m => m.Value.Trim()).ToArray();
        Assert.True(hardcoded.Length == 0,
            "PLC 状态又被写死成 Offline，下面的 if (Online) 分支将永不执行："
            + Environment.NewLine + string.Join(Environment.NewLine, hardcoded));

        // 并且必须真的读了连接状态
        Assert.Contains("_PLCMethod.IsOnline", code, StringComparison.Ordinal);
    }
    /// <summary>
    /// 头部统计不得再写死：<c>UpdateCount</c> / <c>UpdateRunTime</c> 的参数必须是来自
    /// <c>statistics.Model</c> 的真实值，而不是常量。
    /// <para>
    /// 原先是 <c>UpdateCount(10, 200)</c> 与 <c>UpdateRunTime(…, 800)</c> ——
    /// 而 <c>BulletinBoardView.xaml:32-34</c> 的 <c>OKCount</c> / <c>TotalCount</c>
    /// 绑的正是 <c>StatisicsModel.OK</c> / <c>StatisicsModel.Total</c>，即界面上那两个数字。
    /// </para>
    /// <para>
    /// 同样针对<strong>内容</strong>：常量语法完全合法，编译守卫照绿。
    /// </para>
    /// </summary>
    [Fact]
    public void HeaderStatistics_AreNotHardcoded()
    {
        var path = Directory.GetFiles(StartupFlowDirectory(), "05_*.cs").Single();
        var code = string.Join(Environment.NewLine, File.ReadAllLines(path)
            .Where(l => !l.TrimStart().StartsWith("//")));

        // UpdateCount 的两个实参都必须是 statistics.Model.*，不能是字面量
        Assert.False(Regex.IsMatch(code, @"UpdateCount\(\s*\d+\s*,\s*\d+\s*\)"),
            "UpdateCount 的参数又被写成了常量（界面 OKCount/TotalCount 绑的就是这两个值）");
        // UpdateRunTime 的第二个参数（运行时长）不能再是常量
        Assert.False(Regex.IsMatch(code, @"UpdateRunTime\([^;]*,\s*\d+(\.\d+)?\s*\)"),
            "UpdateRunTime 的运行时长又被写成了常量");

        // 必须真的用上产能统计的字段
        Assert.Contains("UpdateCount(statistics.Model.GoodCount", code, StringComparison.Ordinal);
        Assert.Contains("UpdateRunTime(_AlarmInfoService.TotalAlarmTime, statistics.Model.RunTime",
            code, StringComparison.Ordinal);
    }

    /// <summary>
    /// 用到 <c>statistics.Model</c> 之前必须先查库刷新。
    /// <para>
    /// <c>CapacityStatisticsModel</c> 是<strong>单例上的可变状态</strong>：<c>Resolve</c> 只返回实例，
    /// 不会自动查库。若在 <c>GetStatistics()</c> 之前就读 <c>Model.GoodCount</c>，
    /// 拿到的是上一次（甚至从未查询过）的值 —— 语法与编译都正常，只是数据是陈的。
    /// 这条专门盯这个次序。
    /// </para>
    /// </summary>
    [Fact]
    public void Statistics_Model_IsRefreshed_Before_Being_Read()
    {
        var path = Directory.GetFiles(StartupFlowDirectory(), "05_*.cs").Single();
        var code = string.Join(Environment.NewLine, File.ReadAllLines(path)
            .Where(l => !l.TrimStart().StartsWith("//")));

        var firstRead = code.IndexOf("UpdateCount(statistics.Model.", StringComparison.Ordinal);
        Assert.True(firstRead > 0, "未找到读取 statistics.Model 的位置");

        // 该位置之前必须已经出现过一次刷新
        var beforeRead = code[..firstRead];
        Assert.Contains("statistics.GetStatistics()", beforeRead, StringComparison.Ordinal);

        // 刷新必须发生在设置时间窗之后，否则查的是默认窗口
        var refresh = beforeRead.LastIndexOf("statistics.GetStatistics()", StringComparison.Ordinal);
        var setStart = beforeRead.LastIndexOf("statistics.Model.StartTime", StringComparison.Ordinal);
        Assert.True(setStart >= 0 && setStart < refresh,
            "先 GetStatistics() 再设置时间窗 ⇒ 查的是模型默认窗口，不是今天");
    }
    private static string Describe(Diagnostic[] errors) =>
        string.Join(Environment.NewLine, errors.Select(d => $"      {d.Id}  {d.GetMessage()}"));

    /// <summary>
    /// 三条已知良好的启动脚本必须零编译错误。
    /// <para>
    /// 覆盖 taskproj 的下标 0 / 2 / 3 与对应镜像。下标 1（<c>初始化工具</c>）单独由下一条钉住。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(0)]   // 初始化控件
    [InlineData(1)]   // 初始化工具 —— 本轮把 GlobalData.LogPath 换成 BaseDirectory/logs 才通过的
    [InlineData(2)]   // 初始化硬件
    [InlineData(3)]   // 初始化后台 —— 本轮补了 5 行 using 才通过的
    public async Task StartupScript_Compiles(int index)
    {
        var script = ReadTaskProjectScripts()[index];
        var errors = await CompileAsync(script.Code);
        Assert.True(errors.Length == 0, $"{script.Title} 编译失败：{Environment.NewLine}{Describe(errors)}");
    }

    /// <summary>镜像 <c>.cs</c> 必须与 taskproj 里的同一条一样能编译（镜像才是实际执行的那份）。</summary>
    [Theory]
    [InlineData("01_*.cs")]
    [InlineData("02_*.cs")]
    [InlineData("04_*.cs")]
    [InlineData("05_*.cs")]
    public async Task MirrorScript_Compiles(string pattern)
    {
        var path = Directory.GetFiles(StartupFlowDirectory(), pattern).Single();
        var errors = await CompileAsync(File.ReadAllText(path));
        Assert.True(errors.Length == 0, $"{Path.GetFileName(path)} 编译失败：{Environment.NewLine}{Describe(errors)}");
    }

    /// <summary>
    /// 四条启动脚本必须**全部**编译通过。
    /// <para>
    /// 这一条现在没有独立价值，保留是为了在类注释里记下它曾经的形态：
    /// 修复前 `01` / `04` 通过，`02` 因 <c>GlobalData.LogPath</c>（该成员在 git 历史里从未存在过）
    /// 报 `CS0117`，`05` 因缺 5 行 using 报 10 个 `CS0246`/`CS0103`。
    /// 当时用的是「特征化断言」把 `02` 的错因钉死；`02` 修好后它失去意义，故删除，
    /// 改由上面四条 `InlineData` 统一覆盖。
    /// </para>
    /// </summary>
    [Fact]
    public async Task All_Four_Startup_Scripts_Compile()
    {
        var scripts = ReadTaskProjectScripts();
        Assert.Equal(4, scripts.Count);

        foreach (var script in scripts)
        {
            var errors = await CompileAsync(script.Code);
            Assert.True(errors.Length == 0,
                $"{script.Title} 编译失败：{Environment.NewLine}{Describe(errors)}");
        }
    }
}