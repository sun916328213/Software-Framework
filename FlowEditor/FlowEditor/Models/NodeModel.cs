using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFCustomControls.PropertyGrid;
using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.Services;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes;

/// <summary>节点执行状态。</summary>
public enum NodeStatus
{
    /// <summary>空闲，未执行。</summary>
    Idle,
    /// <summary>正在执行（绿色闪烁高亮）。</summary>
    Running,
    /// <summary>执行成功（深绿色）。</summary>
    Success,
    /// <summary>执行异常（红色）。</summary>
    Error
}

/// <summary>
/// 统一流程节点基类。
/// 每个节点强制内置 4 个固定属性：Name、Value、IN、Out。
/// 支持自定义端口动态增删、底部按钮自动扫描、权限控制。
/// </summary>
public abstract partial class NodeModel : ObservableObject, IPortConfigurable, IPortManageable, IDataBindingProvider, INodeExecutable, INodeFaultPolicy
{
    protected NodeModel()
    {
        var attr = GetType().GetCustomAttribute<FlowToolAttribute>();
        _name = attr?.ToolName ?? GetType().Name;
        ActionButtons.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasActionButtons));
        InitActionButtons();
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
    /// 在 FlowChartService.RunAsync 进入 EnterFlow 时清零；循环回边 BumpRun 不清零，
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

    // ── 自定义端口集合 ──

    /// <summary>自定义输入端口列表（用户手动添加，位于节点左侧）。</summary>
    public ObservableCollection<PortModel> CustomInputPorts { get; } = new();

    /// <summary>自定义输出端口列表（用户手动添加，位于节点右侧，可绑定属性）。</summary>
    public ObservableCollection<PortModel> CustomOutputPorts { get; } = new();

    // ── 端口缓存 ──

    private List<PortModel>? _cachedInputPorts;
    private List<PortModel>? _cachedOutputPorts;

    /// <summary>输入端口列表（内置标准端口 + 自定义端口）。</summary>
    public List<PortModel> InputPorts
    {
        get
        {
            if (_cachedInputPorts == null)
                _cachedInputPorts = BuildInputPorts();
            return _cachedInputPorts;
        }
    }

    /// <summary>输出端口列表（内置标准端口 + 自定义端口）。</summary>
    public List<PortModel> OutputPorts
    {
        get
        {
            if (_cachedOutputPorts == null)
                _cachedOutputPorts = BuildOutputPorts();
            return _cachedOutputPorts;
        }
    }

    private List<PortModel> BuildInputPorts()
    {
        var ports = new List<PortModel>();
        // 内置 Next_IN 端口（仅在 IN 勾选时生成）
        if (IsInEnabled)
        {
            ports.Add(new PortModel
            {
                Id = $"{Id}_Next_IN",
                Name = "Next_IN",
                Direction = PortDirection.Input,
                DataType = PortType.Flow,
                OwnerNodeId = Id,
                Category = PortCategory.Standard
            });
        }
        // 自定义输入端口
        ports.AddRange(CustomInputPorts);
        return ports;
    }

    private List<PortModel> BuildOutputPorts()
    {
        var ports = new List<PortModel>();
        // 内置 Next_Out 端口（仅在 Out 勾选时生成）
        if (IsOutEnabled)
        {
            ports.Add(new PortModel
            {
                Id = $"{Id}_Next_Out",
                Name = "Next_Out",
                Direction = PortDirection.Output,
                DataType = PortType.Flow,
                OwnerNodeId = Id,
                Category = PortCategory.Standard
            });
        }
        // 自定义输出端口
        ports.AddRange(CustomOutputPorts);
        return ports;
    }

    /// <summary>使端口缓存失效（端口变更后调用）。</summary>
    public void InvalidatePorts()
    {
        _cachedInputPorts = null;
        _cachedOutputPorts = null;
        OnPropertyChanged(nameof(InputPorts));
        OnPropertyChanged(nameof(OutputPorts));
    }

    // ── 自定义端口操作 ──

    /// <summary>添加自定义输入端口。</summary>
    public PortModel AddCustomInputPort(string name, string? boundProperty = null)
        => AddCustomPort(name, boundProperty, PortDirection.Input);

    /// <summary>添加自定义输出端口（可绑定节点属性）。</summary>
    public PortModel AddCustomOutputPort(string name, string? boundProperty = null)
        => AddCustomPort(name, boundProperty, PortDirection.Output);

    private PortModel AddCustomPort(string name, string? boundProperty, PortDirection direction)
    {
        var displayName = NormalizePortDisplayName(name, boundProperty);
        var port = new PortModel
        {
            Id = $"{Id}_{(direction == PortDirection.Input ? "CustomIn" : "CustomOut")}_{Guid.NewGuid():N}",
            Name = displayName,
            PropertyName = boundProperty,
            Direction = direction,
            DataType = PortTypeClassifier.FromClrType(
                GetType().GetProperty(boundProperty ?? name)?.PropertyType),
            OwnerNodeId = Id,
            Category = PortCategory.Custom
        };
        GetCustomPorts(direction).Add(port);
        InvalidatePorts();
        return port;
    }

    /// <summary>删除自定义端口。</summary>
    public bool RemoveCustomPort(string portId)
    {
        var inPort = CustomInputPorts.FirstOrDefault(p => p.Id == portId);
        if (inPort != null)
        {
            CustomInputPorts.Remove(inPort);
            RemovePortConnections(portId);
            SynchronizeRemovedPortConfig(inPort);
            InvalidatePorts();
            ConfigChanged?.Invoke();
            return true;
        }
        var outPort = CustomOutputPorts.FirstOrDefault(p => p.Id == portId);
        if (outPort != null)
        {
            CustomOutputPorts.Remove(outPort);
            RemovePortConnections(portId);
            SynchronizeRemovedPortConfig(outPort);
            InvalidatePorts();
            ConfigChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 属性网格内联 Check 不应维护第二份状态；删除端口后根据剩余端口回写配置。
    /// 兼容旧文档中 PropertyName 缺失、以 CLR 属性名作为端口名的情况。
    /// </summary>
    private void SynchronizeRemovedPortConfig(PortModel port)
    {
        var propertyName = port.PropertyName;
        if (string.IsNullOrWhiteSpace(propertyName) && GetType().GetProperty(port.Name) != null)
            propertyName = port.Name;
        if (string.IsNullOrWhiteSpace(propertyName))
            return;

        var hasInput = CustomInputPorts.Any(candidate =>
            candidate.PropertyName == propertyName || candidate.Name == propertyName);
        var hasOutput = CustomOutputPorts.Any(candidate =>
            candidate.PropertyName == propertyName || candidate.Name == propertyName);
        if (hasInput || hasOutput)
            _portConfigs[propertyName] = (hasInput, hasOutput);
        else
            _portConfigs.Remove(propertyName);
    }

    /// <summary>删除引用该端口的连线（避免端口删除后残留悬空连线，阻塞目标端口再次连线）。</summary>
    private void RemovePortConnections(string portId)
    {
        if (portId == null) return;
        var service = Service;
        if (service == null) return;

        var toRemove = service.Connections
            .Where(c => c.SourcePortId == portId || c.TargetPortId == portId)
            .ToList();
        foreach (var conn in toRemove)
            service.Connections.Remove(conn);
        if (toRemove.Count > 0)
            service.Model.IsModified = true;
    }

    // ── IPortManageable 显式实现（接口签名与具体类型不一致的成员）──

    // 属性面板只管理数据端口：控制流（Flow）端口由节点结构决定，
    // 不在自定义端口编辑器中展示/删除（画布、持久化仍走具体的 Custom*Ports 集合，不受影响）。
    System.Collections.IEnumerable IPortManageable.CustomInputPorts => CustomInputPorts.Where(p => p.DataType != PortType.Flow);

    System.Collections.IEnumerable IPortManageable.CustomOutputPorts => CustomOutputPorts.Where(p => p.DataType != PortType.Flow);

    void IPortManageable.AddCustomInputPort(string name) => AddCustomInputPort(name);

    void IPortManageable.AddCustomOutputPort(string name, string? boundProperty) => AddCustomOutputPort(name, boundProperty);

    /// <summary>重命名自定义端口。</summary>
    public bool RenameCustomPort(string portId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        var port = CustomInputPorts.FirstOrDefault(p => p.Id == portId)
                ?? CustomOutputPorts.FirstOrDefault(p => p.Id == portId);
        if (port == null) return false;
        port.Name = newName.Trim();
        InvalidatePorts();
        return true;
    }

    // ── 底部按钮 ──

    /// <summary>底部操作按钮列表（由 [NodeAction] 特性自动扫描生成）。</summary>
    public ObservableCollection<NodeActionButton> ActionButtons { get; } = new();

    /// <summary>是否有底部操作按钮（用于 UI 显示/隐藏）。</summary>
    public bool HasActionButtons => ActionButtons.Count > 0;

    private void InitActionButtons()
    {
        // 沿类型层级（DeclaredOnly）收集 [NodeAction]，支持基类下沉的操作按钮
        for (var type = GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<NodeActionAttribute>();
                if (attr == null) continue;
                ActionButtons.Add(new NodeActionButton
                {
                    Name = attr.ButtonName,
                    IconResource = attr.IconResource,
                    Order = attr.Order,
                    Action = () => method.Invoke(this, null)
                });
            }
        }
        // 按 Order 排序
        var sorted = ActionButtons.OrderBy(b => b.Order).ToList();
        ActionButtons.Clear();
        foreach (var btn in sorted) ActionButtons.Add(btn);
    }

    // ── 端口查找 ──

    /// <summary>根据 ID 查找端口（输入或输出）。</summary>
    public PortModel? FindPortById(string portId)
        => InputPorts.FirstOrDefault(p => p.Id == portId)
           ?? OutputPorts.FirstOrDefault(p => p.Id == portId);

    /// <summary>根据名称查找输入端口。</summary>
    public PortModel? FindInputPort(string name)
        => InputPorts.FirstOrDefault(p => p.Name == name);

    /// <summary>根据名称查找输出端口。</summary>
    public PortModel? FindOutputPort(string name)
        => OutputPorts.FirstOrDefault(p => p.Name == name);

    // ── 输入输出同步 ──

    /// <summary>
    /// 执行前调用：把连线值同步到输入端口。
    /// 由执行引擎在 ExecuteAsync 之前调用。
    /// </summary>
    public void SyncInputValues()
    {
        foreach (var port in InputPorts)
        {
            if (port.Value == null) continue;
            var propName = port.PropertyName ?? port.Name;
            SetPropertyValue(propName, port.Value);
        }
    }

    /// <summary>
    /// 执行后调用：把输出端口绑定属性的值同步到端口。
    /// 由执行引擎在 ExecuteAsync 之后调用。
    /// </summary>
    public void SyncOutputValues()
    {
        var type = GetType();
        foreach (var port in OutputPorts)
        {
            var propName = port.PropertyName ?? port.Name;
            var prop = FindPropertyByName(type, propName);
            if (prop == null) continue;
            port.Value = prop.GetValue(this);
        }
    }

    // ── 反射缓存 ──
    // 端口值同步在每个节点的每次执行都会触发属性查找，
    // 缓存 (类型, 属性名) → PropertyInfo，避免逐层反射扫描。

    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> PropertyLookupCache = new();

    /// <summary>各节点类型可绑定属性名缓存（类型不变则结果恒定）。</summary>
    private static readonly ConcurrentDictionary<Type, string[]> BindablePropertiesCache = new();

    private static PropertyInfo? FindPropertyByName(Type type, string name)
        => PropertyLookupCache.GetOrAdd((type, name), static key =>
        {
            for (var t = key.Type; t != null && t != typeof(object); t = t.BaseType)
            {
                var prop = t.GetProperty(key.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (prop != null) return prop;
            }
            return null;
        });

    private void SetPropertyValue(string name, object? value)
    {
        var prop = FindPropertyByName(GetType(), name);
        if (prop == null || !prop.CanWrite) return;
        try
        {
            prop.SetValue(this, value);
        }
        catch (Exception ex)
        {
            throw new FlowChartTypeMismatchException(
                $"无法将值 '{value}'（类型: {value?.GetType().Name ?? "null"}）" +
                $"写入节点 [{Name}] 的属性 {name}（类型: {prop.PropertyType.Name}）: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有可绑定属性名列表（用于输出端口属性绑定下拉框）。
    /// 结果按节点类型缓存，返回副本防止调用方修改缓存。
    /// </summary>
    public List<string> GetBindableProperties()
    {
        var cached = BindablePropertiesCache.GetOrAdd(GetType(), static type =>
        {
            var props = new List<string> { "Name" };
            var seen = new HashSet<string>(StringComparer.Ordinal) { "Name" };
            // 节点工具常通过多层基类复用领域属性（例如 TCP 连接节点的 DeviceName
            // 定义在 NetConnectNodeBase）。逐层扫描到 NodeModel 为止，既包含工具基类
            // 属性，又不会把端口集合、状态等编辑器基础设施暴露成输出属性。
            for (var current = type; current != null && current != typeof(NodeModel); current = current.BaseType)
            {
                foreach (var prop in current.GetProperties(
                             BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (!prop.CanRead || !seen.Add(prop.Name))
                        continue;
                    props.Add(prop.Name);
                }
            }
            return props.ToArray();
        });
        return new List<string>(cached);
    }

    /// <summary>
    /// 子类实现具体算法逻辑。
    /// 执行引擎会在之前调用 SyncInputValues()，之后调用 SyncOutputValues()。
    /// </summary>
    public abstract Task ExecuteAsync(CancellationToken ct);

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

    // ── IPortConfigurable 实现 ──

    /// <summary>
    /// 属性端口配置字典：key=属性名, value=(是否作为输入端口, 是否作为输出端口)。
    /// 由属性面板通过 PortConfigurable=true 的属性开关驱动。
    /// </summary>
    private readonly Dictionary<string, (bool IsInput, bool IsOutput)> _portConfigs = new();

    /// <summary>端口配置变更事件，属性面板监听此事件同步 UI。</summary>
    public event Action? ConfigChanged;

    /// <inheritdoc />
    public (bool IsInput, bool IsOutput) GetPortConfig(string propertyName)
    {
        if (_portConfigs.TryGetValue(propertyName, out var config))
            return config;

        // 检查是否已有对应的自定义端口（从 .flow 文件反序列化恢复时）
        bool hasIn = CustomInputPorts.Any(p => p.PropertyName == propertyName || p.Name == propertyName);
        bool hasOut = CustomOutputPorts.Any(p => p.PropertyName == propertyName || p.Name == propertyName);
        return (hasIn, hasOut);
    }

    /// <inheritdoc />
    public void SetPortConfig(string propertyName, bool isInput, bool isOutput)
    {
        _portConfigs[propertyName] = (isInput, isOutput);
        var displayName = GetPropertyDisplayName(propertyName);
        UpdatePropertyPort(propertyName, displayName, PortDirection.Input, isInput);
        UpdatePropertyPort(propertyName, displayName, PortDirection.Output, isOutput);

        InvalidatePorts();
        ConfigChanged?.Invoke();
    }

    private void UpdatePropertyPort(
        string propertyName,
        string displayName,
        PortDirection direction,
        bool enabled)
    {
        var ports = GetCustomPorts(direction);
        var existing = ports.FirstOrDefault(p =>
            p.PropertyName == propertyName || p.Name == propertyName);
        if (!enabled)
        {
            if (existing != null)
            {
                RemovePortConnections(existing.Id);
                ports.Remove(existing);
            }
            return;
        }

        if (existing == null)
        {
            ports.Add(new PortModel
            {
                Id = $"{Id}_Property{(direction == PortDirection.Input ? "In" : "Out")}_{propertyName}",
                Name = displayName,
                PropertyName = propertyName,
                Direction = direction,
                DataType = PortTypeClassifier.FromClrType(GetType().GetProperty(propertyName)?.PropertyType),
                OwnerNodeId = Id,
                Category = PortCategory.Custom
            });
            return;
        }

        // 兼容旧流程：早期版本把 CLR 属性名当作显示名，且未记录绑定属性。
        if (string.IsNullOrWhiteSpace(existing.Name) || existing.Name == propertyName)
            existing.Name = displayName;
        existing.PropertyName = propertyName;
    }

    private ObservableCollection<PortModel> GetCustomPorts(PortDirection direction)
        => direction == PortDirection.Input ? CustomInputPorts : CustomOutputPorts;

    private string GetPropertyDisplayName(string propertyName)
    {
        var property = GetType().GetProperty(propertyName);
        var attribute = property?.GetCustomAttribute<NodePropertyAttribute>();
        return string.IsNullOrWhiteSpace(attribute?.DisplayName)
            ? propertyName
            : attribute.DisplayName;
    }

    /// <summary>
    /// 保证端口始终有可见名称：属性绑定端口优先使用 NodeProperty.DisplayName，
    /// 无名且无绑定的历史端口使用统一占位名，避免 UI 出现空白行。
    /// </summary>
    private string NormalizePortDisplayName(string? name, string? boundProperty)
    {
        if (!string.IsNullOrWhiteSpace(name))
            return name;
        if (!string.IsNullOrWhiteSpace(boundProperty))
            return GetPropertyDisplayName(boundProperty);
        return "未命名端口";
    }

    // ── IDataBindingProvider 实现 ──

    /// <summary>
    /// 当前流程图服务实例。由 FlowChartService 在添加节点时设置。
    /// 通过接口依赖，用于在属性面板中查询上游节点和创建连线。
    /// </summary>
    public IFlowChartService? Service { get; set; }

    /// <summary>节点被挂载到画布时回调。由 FlowChartService 在 AddNode 时调用。</summary>
    public virtual void OnAttached(IFlowChartService service) { }

    /// <summary>节点从画布移除时回调。由 FlowChartService 在移除节点时调用。</summary>
    public virtual void OnDetached() { }

    /// <summary>数据绑定记录列表。</summary>
    private readonly List<DataBindingInfo> _dataBindings = new();

    // ── DataBindingsChanged 事件 ──

    private event Action? _dataBindingsChanged;
    private bool _connectionSubscribed;

    event Action? IDataBindingProvider.DataBindingsChanged
    {
        add
        {
            _dataBindingsChanged += value;
            EnsureConnectionSubscription();
        }
        remove => _dataBindingsChanged -= value;
    }

    private void EnsureConnectionSubscription()
    {
        if (_connectionSubscribed) return;
        var service = Service;
        if (service == null)
        {
            System.Diagnostics.Debug.WriteLine($"[NodeModel] EnsureConnectionSubscription: Service is null for {Name}");
            return;
        }
        service.Connections.CollectionChanged += OnConnectionsChanged;
        _connectionSubscribed = true;
        System.Diagnostics.Debug.WriteLine($"[NodeModel] EnsureConnectionSubscription: subscribed to Connections.CollectionChanged for {Name}");
    }

    private void OnConnectionsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[NodeModel] OnConnectionsChanged: {e.Action}, {e.NewItems?.Count ?? 0} new items, node={Name}");
        _dataBindingsChanged?.Invoke();
    }

    /// <summary>获取通过流程连线可反向到达的直接或间接上游节点。</summary>
    IReadOnlyList<UpstreamNodeInfo> IDataBindingProvider.GetUpstreamFlowNodes()
    {
        var service = Service;
        if (service == null) return Array.Empty<UpstreamNodeInfo>();

        var upstreamIds = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(Id);
        while (pending.TryDequeue(out var targetId))
        {
            foreach (var connection in service.Connections.Where(connection => connection.TargetNodeId == targetId))
            {
                if (connection.SourceNodeId == Id || !upstreamIds.Add(connection.SourceNodeId))
                    continue;
                pending.Enqueue(connection.SourceNodeId);
            }
        }

        // 保持画布节点顺序稳定，避免每次打开绑定窗口时条目跳动。
        return service.Nodes
            .Where(nodeVm => upstreamIds.Contains(nodeVm.Id))
            .Select(nodeVm => new UpstreamNodeInfo { NodeId = nodeVm.Id, NodeName = nodeVm.Name })
            .ToList();
    }

    /// <summary>获取指定上游节点的可绑定属性列表。</summary>
    IReadOnlyList<string> IDataBindingProvider.GetUpstreamOutputPorts(string upstreamNodeId)
    {
        var service = Service;
        if (service == null) return Array.Empty<string>();

        var nodeVm = service.FindNode(upstreamNodeId);
        if (nodeVm == null) return Array.Empty<string>();

        // 返回上游节点的可绑定属性列表
        return nodeVm.Model.GetBindableProperties();
    }

    /// <summary>创建数据绑定：将上游属性绑定到本节点输入端口。</summary>
    /// <param name="upstreamNodeId">上游节点ID</param>
    /// <param name="upstreamPropertyName">上游属性名（不是端口名）</param>
    /// <param name="targetPortName">本节点输入端口名</param>
    void IDataBindingProvider.CreateDataBinding(string upstreamNodeId, string upstreamPropertyName, string targetPortName)
    {
        var service = Service;
        if (service == null) return;

        var upstreamVm = service.FindNode(upstreamNodeId);
        if (upstreamVm == null) return;

        // 1. 查找上游是否已有绑定该属性的输出端口
        var outPort = upstreamVm.Model.OutputPorts.FirstOrDefault(p =>
            p.DataType != PortType.Flow && p.PropertyName == upstreamPropertyName);

        // 2. 如果没有绑定该属性的输出端口，自动创建一个
        if (outPort == null)
        {
            outPort = upstreamVm.Model.AddCustomOutputPort(upstreamPropertyName, upstreamPropertyName);
            System.Diagnostics.Debug.WriteLine($"[NodeModel] Auto-created output port '{upstreamPropertyName}' on node '{upstreamVm.Name}'");
        }

        // 3. 查找本节点同名输入端口，不存在时按用户指定名称创建
        var inPort = CustomInputPorts.FirstOrDefault(p => p.Name == targetPortName)
            ?? AddCustomInputPort(targetPortName);

        // 4. 检查是否已存在相同连线
        var exists = service.Connections.Any(c =>
            c.SourcePortId == outPort.Id && c.TargetPortId == inPort.Id);
        if (exists) return;

        // 5. 创建连线
        var conn = new ConnectionModel
        {
            SourceNodeId = upstreamNodeId,
            SourcePortId = outPort.Id,
            TargetNodeId = Id,
            TargetPortId = inPort.Id
        };
        service.Connections.Add(conn);

        // 6. 记录绑定信息
        _dataBindings.Add(new DataBindingInfo
        {
            BindingId = $"{Id}_Binding_{upstreamNodeId}_{inPort.Id}",
            UpstreamNodeId = upstreamNodeId,
            UpstreamNodeName = upstreamVm.Name,
            PropertyName = upstreamPropertyName,
            SourcePortName = outPort.Name,
            TargetPortId = inPort.Id
        });

        // 7. 标记流程图已修改
        service.Model.IsModified = true;
    }

    /// <summary>获取当前所有数据绑定（包括通过对话框创建的和手动连线自动检测的）。</summary>
    IReadOnlyList<DataBindingInfo> IDataBindingProvider.GetDataBindings()
    {
        var result = new List<DataBindingInfo>(_dataBindings);
        var service = Service;
        if (service == null)
        {
            System.Diagnostics.Debug.WriteLine($"[NodeModel] GetDataBindings: Service is null for {Name}");
            return result.AsReadOnly();
        }

        // 从实际连线中自动检测绑定（手动连线的情况）
        var myInputPortIds = InputPorts
            .Where(p => p.DataType != PortType.Flow) // 排除 Flow 端口，只检测数据端口
            .Select(p => p.Id)
            .ToHashSet();

        System.Diagnostics.Debug.WriteLine($"[NodeModel] GetDataBindings: node={Name}, nonFlowInputPortIds=[{string.Join(",", myInputPortIds)}], totalConnections={service.Connections.Count}");

        var connectionsToMe = service.Connections
            .Where(c => myInputPortIds.Contains(c.TargetPortId))
            .ToList();

        System.Diagnostics.Debug.WriteLine($"[NodeModel] GetDataBindings: connectionsToMe count={connectionsToMe.Count}");
        foreach (var conn in connectionsToMe)
            System.Diagnostics.Debug.WriteLine($"    conn: srcNode={conn.SourceNodeId}, srcPort={conn.SourcePortId}, tgtPort={conn.TargetPortId}");

        foreach (var conn in connectionsToMe)
        {
            var upstreamVm = service.FindNode(conn.SourceNodeId);
            if (upstreamVm == null || upstreamVm.Id == Id)
            {
                System.Diagnostics.Debug.WriteLine($"    skip: upstreamVm null or same node");
                continue;
            }

            var srcPort = upstreamVm.FindPort(conn.SourcePortId);
            var targetPort = InputPorts.FirstOrDefault(p => p.Id == conn.TargetPortId);
            if (srcPort == null || targetPort == null)
            {
                System.Diagnostics.Debug.WriteLine($"    skip: srcPort={(srcPort != null ? "OK" : "null")}, targetPort={(targetPort != null ? "OK" : "null")}");
                continue;
            }

            var propName = srcPort.PropertyName ?? srcPort.Name;
            System.Diagnostics.Debug.WriteLine($"    processing: srcNode={upstreamVm.Name}({conn.SourceNodeId}), srcPort={srcPort.Name}({srcPort.Id}), propName={propName}, tgtPort={targetPort.Name}({targetPort.Id})");

            // 跳过该目标端口已有绑定记录的（按端口去重，同一上游属性允许绑定到多个端口）
            bool alreadyExists = result.Any(b => b.TargetPortId == conn.TargetPortId);
            System.Diagnostics.Debug.WriteLine($"    alreadyExists={alreadyExists}, _dataBindings.Count={_dataBindings.Count}");
            if (alreadyExists)
                continue;

            result.Add(new DataBindingInfo
            {
                BindingId = $"auto_{Id}_{conn.SourceNodeId}_{conn.TargetPortId}",
                UpstreamNodeId = conn.SourceNodeId,
                UpstreamNodeName = upstreamVm.Name,
                PropertyName = propName,
                SourcePortName = srcPort.Name,
                TargetPortId = conn.TargetPortId
            });
            System.Diagnostics.Debug.WriteLine($"    => added auto-binding: {upstreamVm.Name}.{propName} -> {targetPort.Name}");
        }

        return result.AsReadOnly();
    }

    /// <summary>删除数据绑定及其关联的端口和连线。</summary>
    void IDataBindingProvider.RemoveDataBinding(string bindingId)
    {
        // 先尝试从 _dataBindings 查找，找不到则从连线检测中查找
        var binding = _dataBindings.FirstOrDefault(b => b.BindingId == bindingId);
        if (binding == null)
        {
            // 自动检测的绑定：从 GetDataBindings() 中查找
            binding = ((IDataBindingProvider)this).GetDataBindings()
                .FirstOrDefault(b => b.BindingId == bindingId);
        }
        if (binding == null) return;

        var service = Service;
        if (service == null) return;

        // 删除关联的连线（只删该绑定目标端口的连线，避免误删同一上游的其他绑定）
        var myInputPortIds = InputPorts.Select(p => p.Id).ToHashSet();
        var toRemove = service.Connections
            .Where(c => c.SourceNodeId == binding.UpstreamNodeId &&
                        c.TargetNodeId == Id &&
                        (string.IsNullOrEmpty(binding.TargetPortId)
                            ? myInputPortIds.Contains(c.TargetPortId)
                            : c.TargetPortId == binding.TargetPortId))
            .ToList();
        foreach (var conn in toRemove)
            service.Connections.Remove(conn);

        // 解除绑定：仅删除连线和绑定记录，保留输入端口（变为未绑定状态）
        _dataBindings.RemoveAll(b => b.BindingId == bindingId);

        service.Model.IsModified = true;
    }

    // ── 脚本编辑器命令（子类可重写以支持 PropertyGrid 的 ExtraActions 扩展按钮） ──

    /// <summary>打开脚本编辑器命令。子类重写后可被 PropertyGridHost 自动检测为扩展按钮。</summary>
    [PropertyGridAction("打开脚本编辑器")]
    public virtual ICommand? OpenScriptEditorCommand => null;
}

/// <summary>节点底部操作按钮模型。</summary>
public class NodeActionButton
{
    /// <summary>按钮显示名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>按钮图标。</summary>
    public string IconResource { get; set; } = "DotsHorizontal";

    /// <summary>排序权重。</summary>
    public int Order { get; set; }

    /// <summary>点击执行方法。</summary>
    public Action? Action { get; set; }

    /// <summary>点击命令（包装 Action 用于 WPF 绑定）。</summary>
    public ICommand? Command => Action != null ? new RelayCommand(Action) : null;
}
