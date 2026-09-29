using System.Text.Json;
using FlowEditor.Core;
using FlowEditor.Models;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>
/// 条件分发节点：根据输入值匹配分发规则，决定走哪一个输出分支。
/// <para>
/// <b>用法示例</b>：上游把数据连入「输入值」，分发规则写
/// <c>1=OUT1</c>、<c>2=OUT2</c>，输入 1 时只激活 OUT1 分支，
/// 输入 2 时只激活 OUT2 分支；输入为空或没有规则命中时节点报错。
/// </para>
/// <para>
/// 激活边语义：每次执行只激活命中（或默认）的一个 Flow 端口，
/// 其余分支的子图被引擎整体跳过；命中端口无出线时同样剪枝（旧图回退逻辑只针对
/// 控制流端口整体无出线的情形，本节点新建即带分支端口，不受影响）。
/// </para>
/// <para>
/// 自定义配置统一通过“编辑分发规则”窗口维护；保存后自动同步 Flow 输出端口，
/// 已存在连线的废弃端口会被安全保留，但不再被规则激活。
/// </para>
/// </summary>
[FlowTool("条件分发", "Shuffle", ToolGroup = "流程控制", SortIndex = 3,
    Description = "按输入值匹配自定义规则；输入为空时终止全部下游")]
public class SwitchNode : NodeModel
{
    private object? _inputValue;

    /// <summary>参与匹配的输入值（可由上游任意类型输出绑定，未绑定时用面板常量）。</summary>
    [NodeProperty(DisplayName = "输入值", Group = "1.基础信息", Order = 1, PortConfigurable = false,
        Description = "参与分支匹配的输入值，可由上游节点绑定；面板值在未绑定时生效")]
    public object? InputValue
    {
        get => _inputValue;
        set => SetProperty(ref _inputValue, value);
    }

    private string _rulesText = "1=OUT1\n2=OUT2";

    /// <summary>
    /// 分发规则（每行一条 <c>匹配值=分支名</c>，也可用冒号分隔；# 开头为注释）。
    /// 右侧分支名不存在时会自动创建对应的 Flow 输出端口。
    /// </summary>
    [NodeProperty(DisplayName = "分发规则", Group = "2.分发配置", Order = 1, PortConfigurable = false, IsDisplay = false,
        Description = "每行一条：匹配值=分支名（如 1=OUT1）。未命中时节点报错")]
    public string RulesText
    {
        get => _rulesText;
        set
        {
            if (SetProperty(ref _rulesText, value ?? string.Empty))
                EnsurePortsFromRules();
        }
    }

    private int _branchCount = 2;

    /// <summary>编号分支数量（OUT1..OUTn），增减时自动补齐/回收编号端口。</summary>
    [NodeProperty(DisplayName = "分支数量", Group = "2.分发配置", Order = 0, Min = 2, Max = 12, Step = 1, PortConfigurable = false, IsDisplay = false,
        Description = "OUT1..OUTn 编号分支的数量；自定义名称分支不受此数量影响")]
    public int BranchCount
    {
        get => _branchCount;
        set
        {
            var clamped = Math.Clamp(value, 2, 12);
            if (SetProperty(ref _branchCount, clamped))
            {
                EnsureNumberedBranches(clamped);
                PruneNumberedBranches(clamped);
            }
        }
    }

    private bool _ignoreCase = true;

    /// <summary>匹配时是否忽略大小写（纯数字匹配不受影响）。</summary>
    [NodeProperty(DisplayName = "忽略大小写", Group = "2.分发配置", Order = 2, IsDisplay = false,
        Description = "匹配值比较时是否忽略大小写")]
    public bool IgnoreCase
    {
        get => _ignoreCase;
        set => SetProperty(ref _ignoreCase, value);
    }

    private bool _trimInput = true;

    /// <summary>匹配前是否去除输入值首尾空白。</summary>
    [NodeProperty(DisplayName = "去除首尾空格", Group = "2.分发配置", Order = 3, IsDisplay = false,
        Description = "匹配前是否去除输入值与规则两侧的首尾空白")]
    public bool TrimInput
    {
        get => _trimInput;
        set => SetProperty(ref _trimInput, value);
    }

    private bool _useDefaultBranch;

    /// <summary>旧流程兼容字段；当前版本未命中规则统一报错。</summary>
    [NodeProperty(DisplayName = "启用默认分支", Group = "2.分发配置", Order = 4, IsDisplay = false,
        Description = "旧流程兼容字段，当前版本未命中规则统一报错")]
    public bool UseDefaultBranch
    {
        get => _useDefaultBranch;
        set => SetProperty(ref _useDefaultBranch, value);
    }

    private string _lastMatchedBranch = string.Empty;

    /// <summary>上次执行命中的分支名（运行状态展示，复位/未执行时为空）。</summary>
    [NodeProperty(DisplayName = "上次命中分支", Group = "3.运行状态", Order = 1, ReadOnly = 1, PortConfigurable = false,
        Description = "上次执行实际激活的分支名，未命中且关闭默认分支时显示（未命中）")]
    public string LastMatchedBranch
    {
        get => _lastMatchedBranch;
        set => SetProperty(ref _lastMatchedBranch, value);
    }

    /// <summary>默认分支端口名（常量，供规则解析与执行共用）。</summary>
    public const string DefaultBranchName = "Default";

    /// <summary>
    /// 历史流程曾使用的空分支端口名；新节点不再显示此端口。
    /// </summary>
    public const string EmptyBranchName = "空分支";

    /// <summary>创建具有输入端口和默认编号分支的条件分发节点。</summary>
    public SwitchNode()
    {
        Name = "条件分发";
        IsInEnabled = true;
        // 不再生成通用 Out 端口，以各规则分支端口表达分发走向
        IsOutEnabled = false;

        // 输入值接受任意上游类型：object 映射为 Object，手动放宽为 Any，
        // 避免 int→string 这类跨类型绑定在连线校验期就被 E-TYPE 拦截。
        var input = AddCustomInputPort("输入值", nameof(InputValue));
        input.DataType = PortType.Any;

        EnsureNumberedBranches(_branchCount);
        RemoveUnwiredReservedPort(DefaultBranchName);
        RemoveLegacyEmptyBranchPort();
    }

    /// <inheritdoc />
    public override Task ExecuteAsync(CancellationToken ct)
    {
        // 规则右侧写了新分支名时自动补端口，保证 ActivateOutput 的目标端口真实存在
        EnsurePortsFromRules();

        var inputText = InputToString(InputValue);
        if (string.IsNullOrWhiteSpace(inputText))
        {
            LastMatchedBranch = "(错误：输入为空)";
            throw new InvalidOperationException($"条件分发节点 [{Name}] 的输入值为空，无法匹配分发规则");
        }

        var key = NormalizeKey(inputText);
        foreach (var (match, branch) in ParseRules(RulesText))
        {
            if (NormalizeKey(match) == key)
            {
                LastMatchedBranch = branch;
                ActivateOutput(branch);
                return Task.CompletedTask;
            }
        }

        LastMatchedBranch = "(错误：未命中)";
        throw new InvalidOperationException(
            $"条件分发节点 [{Name}] 的输入值 [{inputText}] 没有匹配的分发规则");
    }

    /// <summary>
    /// 控制流端口 = 全部可见 Flow 输出端口（动态自定义分支）。
    /// </summary>
    public override IReadOnlyList<string>? ControlFlowPorts
        => CustomOutputPorts
            .Where(p => p.DataType == PortType.Flow)
            .Select(p => p.Name).ToList();

    #region 分支端口管理

    /// <summary>解析规则文本为（匹配值，分支名）序列，跳过空行与注释行。</summary>
    public static List<(string Match, string Branch)> ParseRules(string? rulesText)
    {
        var result = new List<(string Match, string Branch)>();
        if (string.IsNullOrWhiteSpace(rulesText))
            return result;
        foreach (var rawLine in rulesText.Split(['\r', '\n', ';', '；'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//"))
                continue;
            var sep = line.IndexOf('=');
            if (sep < 0)
                sep = line.IndexOf(':');
            if (sep <= 0)
                continue;
            var match = line.Substring(0, sep).Trim();
            var branch = line.Substring(sep + 1).Trim();
            if (match.Length == 0 || branch.Length == 0)
                continue;
            result.Add((match, branch));
        }
        return result;
    }

    /// <summary>按匹配选项归一化单个匹配键。</summary>
    private string NormalizeKey(string value)
    {
        var v = TrimInput ? value.Trim() : value;
        return IgnoreCase ? v.ToUpperInvariant() : v;
    }

    /// <summary>
    /// 输入值转匹配字符串：.flow 反序列化后 InputValue 可能为 JsonElement，
    /// 字符串需用 GetString 解包（直接 ToString 会带上 JSON 引号导致匹配失败）。
    /// </summary>
    public static string InputToString(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        JsonElement je => je.ValueKind == JsonValueKind.String
            ? je.GetString() ?? string.Empty
            : je.ToString(),
        _ => value.ToString() ?? string.Empty
    };

    /// <summary>确保分支名对应的 Flow 输出端口存在，不存在则创建并返回。</summary>
    private PortModel EnsureFlowPort(string branchName)
    {
        var port = CustomOutputPorts.FirstOrDefault(p => p.Name == branchName);
        if (port == null)
        {
            port = AddCustomOutputPort(branchName);
            port.DataType = PortType.Flow;
        }
        else if (port.DataType != PortType.Flow)
        {
            port.DataType = PortType.Flow;
        }
        return port;
    }

    /// <summary>补齐 OUT1..OUTn 编号分支端口（已存在则跳过）。</summary>
    private void EnsureNumberedBranches(int count)
    {
        for (var i = 1; i <= count; i++)
            EnsureFlowPort($"OUT{i}");
    }

    /// <summary>为规则右侧出现的所有分支名补齐端口（自定义分支名的来源）。</summary>
    private void EnsurePortsFromRules()
    {
        foreach (var (_, branch) in ParseRules(RulesText))
            EnsureFlowPort(branch);
    }

    /// <summary>删除旧流程遗留且未接线的空分支端口。</summary>
    private void RemoveLegacyEmptyBranchPort()
    {
        var port = CustomOutputPorts.FirstOrDefault(candidate => candidate.Name == EmptyBranchName);
        if (port != null && !IsPortWired(port.Id))
            CustomOutputPorts.Remove(port);
    }

    /// <summary>删除指定名称且未被连线引用的保留端口。</summary>
    /// <param name="portName">待检查的端口名称。</param>
    private void RemoveUnwiredReservedPort(string portName)
    {
        var port = CustomOutputPorts.FirstOrDefault(candidate => candidate.Name == portName);
        if (port != null && !IsPortWired(port.Id))
            CustomOutputPorts.Remove(port);
    }

    /// <summary>从规则编辑窗口一次性提交配置，并同步实际输出端口。</summary>
    public void ApplyRuleConfiguration(
        IReadOnlyList<(string Match, string Branch)> rules,
        bool ignoreCase,
        bool trimInput)
    {
        UseDefaultBranch = false;
        IgnoreCase = ignoreCase;
        TrimInput = trimInput;
        SetProperty(ref _branchCount, Math.Clamp(rules.Count, 2, 12), nameof(BranchCount));
        RulesText = string.Join("\n", rules.Select(rule => $"{rule.Match}={rule.Branch}"));

        var desiredPorts = rules.Select(rule => rule.Branch).ToHashSet(StringComparer.Ordinal);
        foreach (var port in CustomOutputPorts
                     .Where(port => port.DataType == PortType.Flow && !desiredPorts.Contains(port.Name))
                     .ToList())
        {
            // 编辑规则不能破坏现有流程连线；已接线的废弃端口保留，但不会再被激活。
            if (!IsPortWired(port.Id))
                CustomOutputPorts.Remove(port);
        }

        foreach (var name in desiredPorts)
            EnsureFlowPort(name);
        RemoveUnwiredReservedPort(DefaultBranchName);
        RemoveLegacyEmptyBranchPort();
        InvalidatePorts();
        if (Service != null)
            Service.Model.IsModified = true;
    }

    /// <summary>端口是否仍被画布连线引用（收缩分支数量时避免误删已接线端口）。</summary>
    private bool IsPortWired(string portId)
    {
        var service = Service;
        if (service == null)
            return false;
        return service.Connections.Any(c => c.SourcePortId == portId || c.TargetPortId == portId);
    }

    /// <summary>收缩编号分支：仅删除超出数量且未接线的 OUTn 端口，自定义名端口永不自动删除。</summary>
    private void PruneNumberedBranches(int keepCount)
    {
        for (var i = 12; i > keepCount; i--)
        {
            var port = CustomOutputPorts.FirstOrDefault(p => p.Name == $"OUT{i}");
            if (port == null)
                continue;
            if (IsPortWired(port.Id))
                continue;
            CustomOutputPorts.Remove(port);
        }
        InvalidatePorts();
    }

    #endregion

    #region 画布操作按钮

    /// <summary>请求宿主打开当前节点的分发规则编辑器。</summary>
    [NodeAction("编辑分发规则", Order = 0, IconResource = "TableEdit")]
    public void EditDispatchRules()
    {
        Service?.NodeEditorLauncher.Open(this);
    }

    #endregion
}
