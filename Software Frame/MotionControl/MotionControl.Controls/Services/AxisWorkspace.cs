// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：AxisWorkspace、RefreshSubscription。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Runtime.CompilerServices、System.Windows.Threading、MotionControl.Controls.Models、MotionControl.Core.Abstractions、MotionControl.Core.Definitions。导入不代表所有类型都会在运行时加载。
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using MotionControl.Controls.Models;
using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;

namespace MotionControl.Controls.Services;

/// <summary>单轴操作与刷新服务；同一卡/轴共享状态，多个展示订阅合并到一个计时器。</summary>
public sealed class AxisWorkspace
{
    private static readonly ConditionalWeakTable<IMotionCard, Dictionary<int, AxisWorkspace>> Workspaces = new();
    private readonly IMotionCard? _card;
    private readonly Dictionary<long, int> _subscriptions = [];
    private DispatcherTimer? _timer;
    private long _nextSubscription;

    /// <summary>
    /// 初始化 AxisWorkspace 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 UpdateConnection；具体条件和先后次序见方法体。
    /// </summary>
    private AxisWorkspace(IMotionCard? card, int axis)
    {
        _card = card;
        Model = new AxisStateModel(axis);
        UpdateConnection();
    }

    public AxisStateModel Model { get; }
    public int RefreshSubscriberCount => _subscriptions.Count;

    /// <summary>兼容独立控件的卡/轴入口；空卡不进入共享缓存。</summary>
    public static AxisWorkspace For(IMotionCard? card, int axis)
    {
        if (card is null) return new AxisWorkspace(null, axis);
        var axes = Workspaces.GetValue(card, _ => []);
        lock (axes)
        {
            if (!axes.TryGetValue(axis, out var workspace))
                axes[axis] = workspace = new AxisWorkspace(card, axis);
            return workspace;
        }
    }

    /// <summary>在 UI Dispatcher 上订阅刷新；最后一个展示卸载后停止计时器。</summary>
    public IDisposable ObserveRefresh(int intervalMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalMs);
        _timer ??= CreateTimer();
        _timer.Dispatcher.VerifyAccess();
        var id = ++_nextSubscription;
        _subscriptions.Add(id, intervalMs);
        UpdateTimer();
        Refresh();
        return new RefreshSubscription(this, id);
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateTimer。
    /// 正文中的关键协作调用包括 Refresh；具体条件和先后次序见方法体。
    /// </summary>
    private DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background);
        timer.Tick += (_, _) => Refresh();
        return timer;
    }

    /// <summary>
    /// 更新当前实现管理的数据或状态；对应入口为 UpdateTimer。
    /// 正文中的关键协作调用包括 FromMilliseconds、Min、Start；具体条件和先后次序见方法体。
    /// </summary>
    private void UpdateTimer()
    {
        if (_subscriptions.Count == 0) { _timer?.Stop(); return; }
        _timer!.Interval = TimeSpan.FromMilliseconds(_subscriptions.Values.Min());
        _timer.Start();
    }

    /// <summary>
    /// 执行移除操作；对应入口为 RemoveSubscription。
    /// 正文中的关键协作调用包括 VerifyAccess、Remove、UpdateTimer；具体条件和先后次序见方法体。
    /// </summary>
    private void RemoveSubscription(long id)
    {
        _timer!.Dispatcher.VerifyAccess();
        _subscriptions.Remove(id);
        UpdateTimer();
    }

    /// <summary>
    /// RefreshSubscription 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
    /// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class RefreshSubscription(AxisWorkspace workspace, long id) : IDisposable
    {
        private AxisWorkspace? _workspace = workspace;
        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 RemoveSubscription；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose()
        {
            var current = _workspace;
            if (current is null) return;
            current.RemoveSubscription(id);
            _workspace = null;
        }
    }

    /// <summary>
    /// 更新当前实现管理的数据或状态；对应入口为 UpdateConnection。
    /// </summary>
    private void UpdateConnection()
    {
        Model.IsAvailable = _card is not null && Model.Axis >= 0 && Model.Axis < _card.AxisCount;
        Model.IsConnected = _card?.IsConnected == true;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Refresh。
    /// 正文中的关键协作调用包括 UpdateConnection、ResetReadings、ReadCommandPosition、ReadEncoderPosition、ReadAxisStatus；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public void Refresh()
    {
        UpdateConnection();
        if (!Model.IsAvailable || !Model.IsConnected) { ResetReadings(); return; }
        try
        {
            var command = _card!.ReadCommandPosition(Model.Axis);
            Model.CommandPosition = command.IsSuccess ? command.Value : null;
            var encoder = _card.ReadEncoderPosition(Model.Axis);
            Model.EncoderPosition = encoder.IsSuccess ? encoder.Value : null;
            var status = _card.ReadAxisStatus(Model.Axis);
            if (!status.IsSuccess)
            {
                ResetFlags();
                Model.LastMessage = status.Message ?? status.Code.ToString();
                return;
            }
            Model.Enabled = status.Value.Enabled;
            Model.Moving = status.Value.Moving;
            Model.InPosition = status.Value.InPosition;
            Model.Alarm = status.Value.Alarm;
            Model.Homed = status.Value.Homed;
            Model.PositiveLimit = status.Value.PositiveLimit;
            Model.NegativeLimit = status.Value.NegativeLimit;
            Model.EmergencyStop = status.Value.EmergencyStop;
        }
        catch (Exception ex)
        {
            ResetReadings();
            Model.LastMessage = $"读取轴状态失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResetReadings。
    /// 正文中的关键协作调用包括 ResetFlags；具体条件和先后次序见方法体。
    /// </summary>
    private void ResetReadings()
    {
        Model.CommandPosition = Model.EncoderPosition = null;
        ResetFlags();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResetFlags。
    /// </summary>
    private void ResetFlags()
    {
        Model.Enabled = Model.Moving = Model.InPosition = Model.Alarm = Model.Homed = false;
        Model.PositiveLimit = Model.NegativeLimit = Model.EmergencyStop = false;
    }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetServo。
    /// 正文中的关键协作调用包括 Execute、ServoOnResult、ServoOffResult；具体条件和先后次序见方法体。
    /// </summary>
    public MotionResult SetServo(bool on) => Execute(c => on ? c.ServoOnResult(Model.Axis) : c.ServoOffResult(Model.Axis), "伺服使能");
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 JogStart。
    /// 正文中的关键协作调用包括 Execute、JogStartResult；具体条件和先后次序见方法体。
    /// </summary>
    public MotionResult JogStart(EDirection direction) => Execute(c => c.JogStartResult(Model.Axis, direction), "开始点动");
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Stop。
    /// 正文中的关键协作调用包括 Execute、StopResult；具体条件和先后次序见方法体。
    /// </summary>
    public MotionResult Stop(EStopMode mode = EStopMode.Decelerate) => Execute(c => c.StopResult(Model.Axis, mode), "停止单轴");
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 StartHome。
    /// 正文中的关键协作调用包括 Execute、StartHomeResult；具体条件和先后次序见方法体。
    /// </summary>
    public MotionResult StartHome() => Execute(c => c.StartHomeResult(Model.Axis), "启动回零");
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ClearAlarm。
    /// 正文中的关键协作调用包括 Execute、ClearAxisStatusResult；具体条件和先后次序见方法体。
    /// </summary>
    public MotionResult ClearAlarm() => Execute(c => c.ClearAxisStatusResult(Model.Axis), "清除轴状态");
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetSpeed。
    /// 正文中的关键协作调用包括 Execute、SetSpeedResult；具体条件和先后次序见方法体。
    /// </summary>
    public MotionResult SetSpeed(double velocity) => Execute(c => c.SetSpeedResult(Model.Axis, velocity), "设置轴速度");

    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 Execute。
    /// 正文中的关键协作调用包括 UpdateConnection、Finish、Failure、operation、Refresh；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private MotionResult Execute(Func<IMotionCard, MotionResult> operation, string name)
    {
        UpdateConnection();
        if (!Model.IsAvailable) return Finish(MotionResult.Failure(EMotionResult.InvalidParam, $"{name}失败：轴 {Model.Axis} 不可用。"));
        if (!Model.IsConnected) return Finish(MotionResult.Failure(EMotionResult.NotConnected, $"{name}失败：控制卡未连接。"));
        MotionResult result;
        try { result = operation(_card!); }
        catch (Exception ex) { result = MotionResult.Failure(EMotionResult.Fail, $"{name}失败：{ex.Message}"); }
        Finish(result);
        Refresh();
        return result;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Finish。
    /// 正文中的关键协作调用包括 ToString；具体条件和先后次序见方法体。
    /// </summary>
    private MotionResult Finish(MotionResult result)
    {
        Model.LastMessage = result.IsSuccess ? string.Empty : result.Message ?? result.Code.ToString();
        return result;
    }
}
