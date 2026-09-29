using System.ComponentModel;
using System.Runtime.CompilerServices;
using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;

namespace MotionControl.Controls.ViewModels;

/// <summary>
/// 单轴展示层模型：只面向 <see cref="IMotionCard"/> 通用契约轮询与操作，不引用任何具体厂商类型。
/// <para>线程说明：内部仅做同步读取（厂商基类自带锁），由视图侧 DispatcherTimer 在 UI 线程驱动 <see cref="Refresh"/>。</para>
/// </summary>
public sealed class AxisViewModel : INotifyPropertyChanged
{
    private readonly IMotionCard? _card;
    private double? _commandPosition;
    private double? _encoderPosition;
    private bool _enabled;
    private bool _moving;
    private bool _inPosition;
    private bool _alarm;
    private bool _homed;
    private bool _positiveLimit;
    private bool _negativeLimit;
    private bool _emergencyStop;
    private string _lastMessage = string.Empty;

    /// <summary>创建单轴视图模型。</summary>
    /// <param name="card">运动控制卡（可为空，为空时显示未绑定）。</param>
    /// <param name="axis">轴索引，从0开始。</param>
    public AxisViewModel(IMotionCard? card, int axis)
    {
        _card = card;
        Axis = axis;
    }

    /// <summary>轴索引。</summary>
    public int Axis { get; }

    /// <summary>轴是否可用（卡非空且索引合法）。</summary>
    public bool IsAvailable => _card != null && Axis >= 0 && Axis < _card.AxisCount;

    /// <summary>控制卡是否已连接。</summary>
    public bool IsConnected => _card?.IsConnected == true;

    /// <summary>指令位置（用户单位，未连接时为 null）。</summary>
    public double? CommandPosition
    {
        get => _commandPosition;
        private set => SetProperty(ref _commandPosition, value);
    }

    /// <summary>编码器位置（用户单位，未连接时为 null）。</summary>
    public double? EncoderPosition
    {
        get => _encoderPosition;
        private set => SetProperty(ref _encoderPosition, value);
    }

    /// <summary>伺服使能。</summary>
    public bool Enabled
    {
        get => _enabled;
        private set => SetProperty(ref _enabled, value);
    }

    /// <summary>运动中。</summary>
    public bool Moving
    {
        get => _moving;
        private set => SetProperty(ref _moving, value);
    }

    /// <summary>到位。</summary>
    public bool InPosition
    {
        get => _inPosition;
        private set => SetProperty(ref _inPosition, value);
    }

    /// <summary>报警。</summary>
    public bool Alarm
    {
        get => _alarm;
        private set => SetProperty(ref _alarm, value);
    }

    /// <summary>已回零。</summary>
    public bool Homed
    {
        get => _homed;
        private set => SetProperty(ref _homed, value);
    }

    /// <summary>正限位。</summary>
    public bool PositiveLimit
    {
        get => _positiveLimit;
        private set => SetProperty(ref _positiveLimit, value);
    }

    /// <summary>负限位。</summary>
    public bool NegativeLimit
    {
        get => _negativeLimit;
        private set => SetProperty(ref _negativeLimit, value);
    }

    /// <summary>急停。</summary>
    public bool EmergencyStop
    {
        get => _emergencyStop;
        private set => SetProperty(ref _emergencyStop, value);
    }

    /// <summary>最近一次操作的结果说明（成功为空）。</summary>
    public string LastMessage
    {
        get => _lastMessage;
        private set => SetProperty(ref _lastMessage, value);
    }

    /// <summary>状态汇总行。</summary>
    public string StatusSummary
    {
        get
        {
            if (!IsAvailable)
                return "轴不可用";
            if (!IsConnected)
                return "未连接";
            var parts = new List<string>();
            if (Enabled) parts.Add("ServoOn");
            if (Moving) parts.Add("运动中");
            if (InPosition) parts.Add("到位");
            if (Alarm) parts.Add("报警");
            if (Homed) parts.Add("已回零");
            if (PositiveLimit) parts.Add("正限位");
            if (NegativeLimit) parts.Add("负限位");
            if (EmergencyStop) parts.Add("急停");
            return parts.Count == 0 ? "空闲" : string.Join(" / ", parts);
        }
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>从设备刷新一次状态（失败时读数清 null，不抛异常）。</summary>
    public void Refresh()
    {
        if (!IsAvailable || !IsConnected)
        {
            CommandPosition = null;
            EncoderPosition = null;
            Enabled = Moving = InPosition = Alarm = Homed = PositiveLimit = NegativeLimit = EmergencyStop = false;
            OnPropertyChanged(nameof(StatusSummary));
            return;
        }

        var card = _card!;
        var command = card.ReadCommandPosition(Axis);
        CommandPosition = command.IsSuccess ? command.Value : null;
        var encoder = card.ReadEncoderPosition(Axis);
        EncoderPosition = encoder.IsSuccess ? encoder.Value : null;
        var status = card.ReadAxisStatus(Axis);
        if (status.IsSuccess)
        {
            Enabled = status.Value.Enabled;
            Moving = status.Value.Moving;
            InPosition = status.Value.InPosition;
            Alarm = status.Value.Alarm;
            Homed = status.Value.Homed;
            PositiveLimit = status.Value.PositiveLimit;
            NegativeLimit = status.Value.NegativeLimit;
            EmergencyStop = status.Value.EmergencyStop;
        }
        OnPropertyChanged(nameof(StatusSummary));
    }

    /// <summary>伺服开/关，成功后立即刷新一次。</summary>
    public MotionResult SetServo(bool on)
        => Execute(on ? _card?.ServoOnResult(Axis) : _card?.ServoOffResult(Axis),
            on ? "伺服使能" : "伺服失能");

    /// <summary>开始点动，成功后立即刷新一次。</summary>
    public MotionResult JogStart(EDirection direction)
        => Execute(_card?.JogStartResult(Axis, direction), "开始点动");

    /// <summary>停止单轴，成功后立即刷新一次。</summary>
    public MotionResult Stop(EStopMode stopMode = EStopMode.Decelerate)
        => Execute(_card?.StopResult(Axis, stopMode), "停止单轴");

    /// <summary>启动回零，成功后立即刷新一次。</summary>
    public MotionResult StartHome()
        => Execute(_card?.StartHomeResult(Axis), "启动回零");

    /// <summary>清除轴报警/限位状态，成功后立即刷新一次。</summary>
    public MotionResult ClearAlarm()
        => Execute(_card?.ClearAxisStatusResult(Axis), "清除轴状态");

    /// <summary>设置轴运行速度（用户单位/s）。</summary>
    public MotionResult SetSpeed(double velocity)
        => Execute(_card?.SetSpeedResult(Axis, velocity), "设置轴速度");

    private MotionResult Execute(MotionResult? result, string operation)
    {
        if (!IsAvailable)
            return Finish(MotionResult.Failure(EMotionResult.InvalidParam, $"{operation}失败：轴 {Axis} 不可用。"));
        if (!IsConnected)
            return Finish(MotionResult.Failure(EMotionResult.NotConnected, $"{operation}失败：控制卡未连接。"));
        if (result is null)
            return Finish(MotionResult.Failure(EMotionResult.Fail, $"{operation}失败：未知错误。"));
        var finished = Finish(result.Value);
        Refresh();
        return finished;
    }

    private MotionResult Finish(MotionResult result)
    {
        LastMessage = result.IsSuccess ? string.Empty : result.Message ?? result.Code.ToString();
        return result;
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
