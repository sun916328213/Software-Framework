using FlowEditor.Core;
using FlowEditor.Models;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>
/// 循环节点：计数循环，执行 Body 分支指定次数后走 Completed。
/// <para>
/// <b>用法（无需回边）</b>：Body → 循环体第一个节点 → … → 循环体末端节点<b>悬空即可</b>；
/// Completed → 循环结束后的下游。<see cref="LoopNormalizer"/> 会在编译期为循环体末端
/// 自动合成回边，画布上不再需要横跨整图的回边连线。
/// 若已手动把末端接回本节点的 Next_IN（老图），自动合成会跳过该末端，行为不变。
/// </para>
/// <para>
/// 每次被调度（含回边重入）CurrentIndex +1：未达 Iterations 时激活 Body，
/// 达到后激活 Completed 并自动复位计数，环路自然终止。
/// 旧图兼容：Body/Completed 无出线时（旧图仅用 Next_Out 直通），由引擎回退为全放行。
/// </para>
/// </summary>
[FlowTool("循环", "Sync", ToolGroup = "流程控制", SortIndex = 4, Description = "循环执行 Body 分支指定次数后走 Completed（循环体末端无需接回本节点）")]
public class LoopNode : NodeModel, ILoopControlNode
{
    private readonly List<PortModel> _bodyPorts = new();

    private int _iterations = 5;

    /// <summary>循环次数（可由上游节点通过输入端口绑定，未绑定时用面板常量）。</summary>
    [NodeProperty(DisplayName = "循环次数", Group = "1.基础信息", Order = 1, Min = 1, Step = 1, IsInput = true,
        Description = "Body 分支重复执行的次数，可由上游节点绑定；面板值在未绑定时生效")]
    public int Iterations
    {
        get => _iterations;
        set => SetProperty(ref _iterations, value);
    }

    private int _currentIndex;

    /// <summary>当前迭代序号（1 起）。循环体可通过输出端口「当前索引」直接取用。</summary>
    [NodeProperty(DisplayName = "当前索引", Group = "1.基础信息", Order = 2, ReadOnly = 1, 
        Description = "当前第几次迭代（从 1 开始），循环结束后复位为 0")]
    public int CurrentIndex
    {
        get => _currentIndex;
        set => SetProperty(ref _currentIndex, value);
    }

    /// <summary>创建具有 Body、Completed 和循环参数端口的计数循环节点。</summary>
    public LoopNode()
    {
        Name = "循环";
        IsInEnabled = true;
        // 新节点默认不再生成通用 Out 端口，以 Body/Completed 表达循环走向
        IsOutEnabled = false;

        // Body/Completed 是控制流端口：声明为 Flow 才能触发连线时的自动数据绑定，
        // 并与 ControlFlowPorts 的激活边语义保持一致
        var body = AddCustomOutputPort("Body");
        body.DataType = PortType.Flow;
        _bodyPorts.Add(body);

        var completed = AddCustomOutputPort("Completed");
        completed.DataType = PortType.Flow;

        // 迭代序号/循环次数默认开放为输出端口，循环体可直接取用，无需手动开端口
        AddCustomOutputPort("当前索引", nameof(CurrentIndex));
        AddCustomOutputPort("循环次数", nameof(Iterations));

        // 循环次数允许由上游绑定（端口名须为属性名，SyncInputValues 按名写回属性）
        AddCustomInputPort("循环次数", nameof(Iterations));
    }

    /// <inheritdoc />
    /// <remarks>动态读取端口 Id：反序列化会用 .flow 中保存的 Id 覆盖，缓存会失效。</remarks>
    public IReadOnlyCollection<string> BodyPortIds
    {
        get
        {
            var ids = new List<string>(_bodyPorts.Count);
            foreach (var port in _bodyPorts) ids.Add(port.Id);
            return ids;
        }
    }

    /// <inheritdoc />
    /// <remarks>指向内置 Next_IN；每次取用动态拼装，避免反序列化后节点 Id 变化导致失效。</remarks>
    public string LoopBackPortId => $"{Id}_Next_IN";

    /// <inheritdoc />
    public override Task ExecuteAsync(CancellationToken ct)
    {
        // 防复活保护（配合引擎队列级回边去重的双保险）：上一轮已走 Completed 后若仍被调度，
        // 说明是队列中的残留调度（多末端并行回边等场景的极端产物）。此时不推进计数，
        // 一律再次激活 Completed 端口走终止分支——激活边语义会剪掉 Body 出边，
        // 保证环路必然排空终止，而非重新进入 Body 模式造成无限活锁。
        if (_completedThisCycle)
        {
            _completedThisCycle = false;
            ActivateOutput("Completed");
            return Task.CompletedTask;
        }

        CurrentIndex++;
        if (CurrentIndex <= Math.Max(1, Iterations))
        {
            ActivateOutput("Body");
        }
        else
        {
            ActivateOutput("Completed");
            CurrentIndex = 0; // 本轮循环结束，复位供下一次外部触发
            _completedThisCycle = true; // 标记本轮已终止，吞掉队列中可能残留的重复调度
        }
        return Task.CompletedTask;
    }

    /// <summary>本轮循环已走 Completed 的标记：用于吞掉多末端回边产生的残留重复调度，防止循环复活。</summary>
    private bool _completedThisCycle;

    /// <inheritdoc />
    public override IReadOnlyList<string>? ControlFlowPorts => new[] { "Body", "Completed" };

    /// <summary>将当前循环索引复位为零。</summary>
    [NodeAction("重置计数", Order = 0, IconResource = "Restart")]
    public void ResetCounter()
    {
        CurrentIndex = 0;
    }

    /// <summary>通过宿主通知 seam 显示当前循环参数。</summary>
    [NodeAction("预览参数", Order = 1, IconResource = "EyeOutline")]
    public void PreviewParams()
    {
        Service?.NodeNotifications.ShowInformation("循环参数预览", GetParameterSummary());
    }

    /// <summary>生成不依赖具体 UI 的循环参数摘要。</summary>
    /// <returns>适合面板、对话框或日志展示的多行文本。</returns>
    public string GetParameterSummary()
        => $"循环次数: {Iterations}\n当前索引: {CurrentIndex}";
}
