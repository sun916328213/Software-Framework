using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using FlowEditor.Core;
using FlowEditor.Models;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes;

/// <summary>
/// 统一流程节点基类。
/// 每个节点提供名称、执行状态、标准流端口和可扩展属性/端口能力。
/// 支持自定义端口动态增删、底部按钮自动扫描、权限控制。
/// </summary>
public abstract partial class NodeModel : ObservableObject, IFlowValidationNode, INodeFaultPolicy, IExecutionScopeRuntimeAware, INodeContextExecutable
{
    /// <summary>初始化节点名称并发现声明式节点动作。</summary>
    protected NodeModel()
    {
        var attr = GetType().GetCustomAttribute<FlowToolAttribute>();
        _name = attr?.ToolName ?? GetType().Name;
        InitActionDescriptors();
    }

    /// <summary>节点唯一标识。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    // ── 4 个内置固定属性 ──

    private string _name = "节点";
    /// <summary>【内置属性1】节点唯一名称，文本输入框；无端口关联。</summary>
    [NodeProperty(DisplayName = "名称", Group = "1.基础信息", Order = 0)]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

     private long _lastElapsedMs;
    /// <summary>最近一次执行耗时（毫秒），运行时由引擎写入，UI 绑定显示</summary>
    public long LastElapsedMs
    {
        get => _lastElapsedMs;
        set => SetProperty(ref _lastElapsedMs, value);
    }

    private int _count;
    /// <summary>工具运行次数历史累计（永不重置），运行时由引擎写入，UI 绑定显示</summary>
    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    private int _currentRunCount;
    /// <summary>
    /// 本次"按执行按钮"周期内该节点被触发的次数（节点局部）。
    /// 在 FlowExecutionCoordinator.RunAsync 进入 EnterFlow 时清零；循环回边 BumpRun 不清零，
    /// 因此循环内第 N 轮执行的节点 CurrentRunCount=N，可直接看出循环跑了多少轮。
    /// 排查"循环节点 Count 偏大"用：独立触发节点 CurrentRunCount=1，循环节点 CurrentRunCount=轮数。
    /// </summary>
    public int CurrentRunCount
    {
        get => _currentRunCount;
        set => SetProperty(ref _currentRunCount, value);
    }

    private int _runSequence;
    /// <summary>
    /// 本次"按执行按钮"周期内的全局执行序号（共享累加器，节点各自持有一份"上一次被触发的全局序号"）。
    /// 同一周期内所有节点共享一个累加器，节点 A 在循环里跑 5 轮后节点 B 执行：
    /// A.RunSequence=1,2,3,4,5；B.RunSequence=6 —— 一眼看出 A 循环 5 轮后 B 才执行。
    /// </summary>
    public int RunSequence
    {
        get => _runSequence;
        set => SetProperty(ref _runSequence, value);
    }

    private bool _isInEnabled=true;
    /// <summary>【内置属性3】IN：勾选自动生成内置 Next_IN 输入端口，取消勾选隐藏该端口。</summary>
    public bool IsInEnabled
    {
        get => _isInEnabled;
        set
        {
            if (SetProperty(ref _isInEnabled, value))
                InvalidatePorts();
        }
    }

    private bool _isOutEnabled = true;
    /// <summary>【内置属性4】Out：勾选自动生成内置 Next_Out 输出端口，取消勾选隐藏该端口。</summary>
    public bool IsOutEnabled
    {
        get => _isOutEnabled;
        set
        {
            if (SetProperty(ref _isOutEnabled, value))
                InvalidatePorts();
        }
    }

    // ── 画布坐标 ──

    private double _x;
    /// <summary>节点画布 X 坐标。</summary>
    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    private double _y;
    /// <summary>节点画布 Y 坐标。</summary>
    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    // ── 执行状态 ──

    private NodeStatus _status = NodeStatus.Idle;
    /// <summary>节点当前执行状态。</summary>
    public NodeStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    // ── 权限控制 ──

    private NodePermission _permission = NodePermission.Normal;
    /// <summary>节点权限：Normal / ReadOnly / Disabled。</summary>
    public NodePermission Permission
    {
        get => _permission;
        set => SetProperty(ref _permission, value);
    }

    /// <inheritdoc />
    public bool RequiresFlowInput => IsInEnabled;

    private bool _isExcludedFromExecution;
    /// <summary>是否存放在“不执行节点容器”中；保留配置，但不参与校验、编译和运行。</summary>
    public bool IsExcludedFromExecution
    {
        get => _isExcludedFromExecution;
        set => SetProperty(ref _isExcludedFromExecution, value);
    }

    private string? _parentExecutionContainerId;
    /// <summary>所属结构化执行容器；null 表示位于主流程作用域。</summary>
    public string? ParentExecutionContainerId
    {
        get => _parentExecutionContainerId;
        set => SetProperty(ref _parentExecutionContainerId, value);
    }

    private string? _executionLaneId;
    /// <summary>所属容器内的泳道标识。</summary>
    public string? ExecutionLaneId
    {
        get => _executionLaneId;
        set => SetProperty(ref _executionLaneId, value);
    }

    /// <summary>当前运行的完整图上下文；仅运行期赋值，不参与保存。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ExecutionScopeRuntime? ExecutionScopeRuntime { get; set; }

    // ── 容错策略（P2）──

    private NodeErrorPolicy _errorPolicy = NodeErrorPolicy.FailFast;
    /// <summary>失败处理策略：FailFast / ContinueOnError / RouteToErrorPort。</summary>
    public NodeErrorPolicy ErrorPolicy
    {
        get => _errorPolicy;
        set => SetProperty(ref _errorPolicy, value);
    }

    private int _retryCount;
    /// <summary>失败自动重试次数（0 = 不重试，最大 10）。</summary>
    public int RetryCount
    {
        get => _retryCount;
        set => SetProperty(ref _retryCount, Math.Clamp(value, 0, 10));
    }

    private int _retryDelayMs;
    /// <summary>重试间隔（毫秒，最大 60000）。</summary>
    public int RetryDelayMs
    {
        get => _retryDelayMs;
        set => SetProperty(ref _retryDelayMs, Math.Clamp(value, 0, 60_000));
    }

    private int _timeoutMs;
    /// <summary>单次执行超时（毫秒），0 = 不限制。超时按失败处理并计入重试。</summary>
    public int TimeoutMs
    {
        get => _timeoutMs;
        set => SetProperty(ref _timeoutMs, Math.Clamp(value, 0, 3_600_000));
    }

    /// <summary>是否可编辑（非只读且非禁用）。</summary>
    public bool IsEditable => Permission == NodePermission.Normal;

    /// <summary>是否可拖动（非禁用）。</summary>
    public bool IsDraggable => Permission != NodePermission.Disabled;

    // ── 节点动作 ──

    private readonly Dictionary<string, MethodInfo> _actionMethods = new(StringComparer.Ordinal);

    /// <summary>节点声明的动作元数据（由 <see cref="NodeActionAttribute"/> 特性自动扫描生成）。</summary>
    public IReadOnlyList<NodeActionDescriptor> ActionDescriptors { get; private set; } = [];

    /// <summary>扫描节点类型层级，建立动作描述与实现方法的内部映射。</summary>
    private void InitActionDescriptors()
    {
        var descriptors = new List<NodeActionDescriptor>();

        // 沿类型层级（DeclaredOnly）收集 [NodeAction]，支持基类下沉的操作按钮
        for (var type = GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<NodeActionAttribute>();
                if (attr == null) continue;
                if (method.GetParameters().Length != 0)
                    throw new InvalidOperationException(
                        $"节点动作 [{type.FullName}.{method.Name}] 不能声明参数。");

                var actionId = $"{type.FullName}.{method.Name}";
                if (!_actionMethods.TryAdd(actionId, method))
                    throw new InvalidOperationException($"节点动作标识重复: {actionId}");
                descriptors.Add(new NodeActionDescriptor(
                    actionId,
                    attr.ButtonName,
                    attr.IconResource,
                    attr.Order));
            }
        }

        ActionDescriptors = descriptors
            .OrderBy(descriptor => descriptor.Order)
            .ThenBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>执行节点已声明的动作。</summary>
    /// <param name="actionId"><see cref="NodeActionDescriptor.Id"/> 中的动作标识。</param>
    /// <exception cref="ArgumentException">动作标识不属于当前节点。</exception>
    public void ExecuteAction(string actionId)
    {
        if (!_actionMethods.TryGetValue(actionId, out var method))
            throw new ArgumentException($"节点 [{Name}] 不存在动作: {actionId}", nameof(actionId));

        method.Invoke(this, null);
    }

    /// <summary>
    /// 子类实现具体算法逻辑。
    /// 执行引擎会在之前调用 SyncInputValues()，之后调用 SyncOutputValues()。
    /// </summary>
    public abstract Task ExecuteAsync(CancellationToken ct);

    /// <summary>
    /// P0 新执行面：默认转调旧 <see cref="ExecuteAsync(CancellationToken)"/>，既有节点零改动兼容。
    /// 新节点应重写此 overload，只从 <paramref name="ctx"/> 获取取消令牌、日志、变量与服务，
    /// 不得直接访问全局 Container/Manager.Instance、Application.Current、Dispatcher 或窗口。
    /// </summary>
    public virtual Task ExecuteAsync(NodeExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        return ExecuteAsync(ctx.CancellationToken);
    }

    Task INodeContextExecutable.ExecuteAsync(NodeExecutionContext ctx) => ExecuteAsync(ctx);

    // ── 激活边（P1 控制流语义）──
    // 节点在 ExecuteAsync 中调用 ActivateOutput 声明本次实际激活的输出端口，
    // 引擎只沿被激活端口的连线继续调度下游；
    // 未调用时保持旧行为（所有出边放行），既有节点零改动兼容。

    private readonly HashSet<string> _activatedOutputs = new();
    private bool _activationReported;

    /// <summary>
    /// 声明本次执行激活的输出端口（按端口名）。
    /// 典型用法：条件分支只激活 True/False 之一；循环节点激活 Body 或 Completed。
    /// </summary>
    protected void ActivateOutput(params string[] portNames)
    {
        foreach (var name in portNames)
            _activatedOutputs.Add(name);
        _activationReported = true;
    }

    /// <summary>引擎在本节点每次执行前调用：清空上一轮激活记录。</summary>
    internal void ResetActivation()
    {
        _activatedOutputs.Clear();
        _activationReported = false;
    }

    /// <summary>本节点是否显式声明了激活端口（false = 未接入控制流语义，全部放行）。</summary>
    internal bool ActivationReported => _activationReported;

    /// <summary>显式激活的输出端口名集合；未声明时返回 null。</summary>
    internal IReadOnlyCollection<string>? GetActivatedOutputNames()
        => _activationReported ? _activatedOutputs : null;

    // ── INodeExecutable 显式实现（返回类型/可见性与接口不一致的成员）──

    IReadOnlyList<PortModel> INodeExecutable.InputPorts => InputPorts;

    IReadOnlyList<PortModel> INodeExecutable.OutputPorts => OutputPorts;

    void INodeExecutable.ResetActivation() => ResetActivation();

    IReadOnlyCollection<string>? INodeExecutable.GetActivatedOutputNames() => GetActivatedOutputNames();

    /// <summary>
    /// 控制流端口名列表。声明后引擎据此识别旧图：
    /// 所有控制流端口均无出线时（旧图仅用通用 Next_Out 直通），忽略激活边过滤保持旧行为。
    /// 非 控制流节点返回 null（激活边语义不适用，正常节点从不调用 ActivateOutput）。
    /// </summary>
    public virtual IReadOnlyList<string>? ControlFlowPorts => null;

    /// <summary>多输入控制流的默认汇聚策略。</summary>
    public virtual FlowJoinMode FlowJoinMode => FlowJoinMode.WaitAllAnyActive;

    /// <summary>
    /// 流程图保存到文件时回调（此时 .flow 文档已写入磁盘）。
    /// 子类可在此持久化自身依赖的外部数据（如视觉工具的 Halcon 参数文件）。
    /// 默认空实现。
    /// </summary>
    public virtual void OnFlowSaved() { }

}
