// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：AxisViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.ComponentModel、System.Globalization、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、MotionControl.Controls.Models、MotionControl.Controls.Services。导入不代表所有类型都会在运行时加载。
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotionControl.Controls.Models;
using MotionControl.Controls.Services;
using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;

namespace MotionControl.Controls.ViewModels;

/// <summary>单轴展示与命令适配；状态和设备操作由 Workspace 持有。</summary>
public sealed class AxisViewModel : ObservableObject
{
    /// <summary>
    /// 初始化 AxisViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 For；具体条件和先后次序见方法体。
    /// </summary>
    public AxisViewModel(IMotionCard? card, int axis) : this(AxisWorkspace.For(card, axis)) { }
    /// <summary>
    /// 初始化 AxisViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof、AddHandler、SetServo、StartHome、ClearAlarm；具体条件和先后次序见方法体。
    /// </summary>
    public AxisViewModel(AxisWorkspace workspace)
    {
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        PropertyChangedEventManager.AddHandler(Model, OnModelChanged, string.Empty);
        ServoOnCommand = new RelayCommand(() => SetServo(true), CanOperate);
        ServoOffCommand = new RelayCommand(() => SetServo(false), CanOperate);
        StartHomeCommand = new RelayCommand(() => StartHome(), CanOperate);
        ClearAlarmCommand = new RelayCommand(() => ClearAlarm(), CanOperate);
        StopCommand = new RelayCommand(() => Stop(), CanOperate);
        JogStartCommand = new RelayCommand<EDirection>(direction => JogStart(direction), _ => CanOperate());
        ApplySpeedCommand = new RelayCommand(ApplySpeed, CanOperate);
    }

    public AxisWorkspace Workspace { get; }
    public AxisStateModel Model => Workspace.Model;
    // 兼容原有绑定及调用方。
    public int Axis => Model.Axis;
    public bool IsAvailable => Model.IsAvailable;
    public bool IsConnected => Model.IsConnected;
    public double? CommandPosition => Model.CommandPosition;
    public double? EncoderPosition => Model.EncoderPosition;
    public bool Enabled => Model.Enabled;
    public bool Moving => Model.Moving;
    public bool InPosition => Model.InPosition;
    public bool Alarm => Model.Alarm;
    public bool Homed => Model.Homed;
    public bool PositiveLimit => Model.PositiveLimit;
    public bool NegativeLimit => Model.NegativeLimit;
    public bool EmergencyStop => Model.EmergencyStop;
    public string LastMessage => Model.LastMessage;
    public string StatusSummary => Model.StatusSummary;
    public string SpeedInput { get => Model.SpeedInput; set => Model.SpeedInput = value; }
    public bool IsOperable => Model.IsAvailable && Model.IsConnected;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanOperate。
    /// </summary>
    private bool CanOperate() => IsOperable;
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnModelChanged。
    /// 正文中的关键协作调用包括 OnPropertyChanged、nameof、NotifyCanExecuteChanged；具体条件和先后次序见方法体。
    /// </summary>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
        if (e.PropertyName is not (nameof(Model.IsAvailable) or nameof(Model.IsConnected))) return;
        OnPropertyChanged(nameof(IsOperable));
        ServoOnCommand.NotifyCanExecuteChanged();
        ServoOffCommand.NotifyCanExecuteChanged();
        StartHomeCommand.NotifyCanExecuteChanged();
        ClearAlarmCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        JogStartCommand.NotifyCanExecuteChanged();
        ApplySpeedCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Refresh。
    /// 正文中的关键协作调用包括 Refresh；具体条件和先后次序见方法体。
    /// </summary>
    public void Refresh() => Workspace.Refresh();
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetServo。
    /// 正文中的关键协作调用包括 SetServo；具体条件和先后次序见方法体。
    /// </summary>
    public MotionResult SetServo(bool on) => Workspace.SetServo(on);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 JogStart。
    /// 正文中的关键协作调用包括 JogStart；具体条件和先后次序见方法体。
    /// </summary>
    public MotionResult JogStart(EDirection direction) => Workspace.JogStart(direction);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Stop。
    /// 正文中的关键协作调用包括 Stop；具体条件和先后次序见方法体。
    /// </summary>
    public MotionResult Stop(EStopMode stopMode = EStopMode.Decelerate) => Workspace.Stop(stopMode);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 StartHome。
    /// 正文中的关键协作调用包括 StartHome；具体条件和先后次序见方法体。
    /// </summary>
    public MotionResult StartHome() => Workspace.StartHome();
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ClearAlarm。
    /// 正文中的关键协作调用包括 ClearAlarm；具体条件和先后次序见方法体。
    /// </summary>
    public MotionResult ClearAlarm() => Workspace.ClearAlarm();
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetSpeed。
    /// 正文中的关键协作调用包括 SetSpeed；具体条件和先后次序见方法体。
    /// </summary>
    public MotionResult SetSpeed(double velocity) => Workspace.SetSpeed(velocity);
    public IRelayCommand ServoOnCommand { get; }
    public IRelayCommand ServoOffCommand { get; }
    public IRelayCommand StartHomeCommand { get; }
    public IRelayCommand ClearAlarmCommand { get; }
    public IRelayCommand StopCommand { get; }
    public IRelayCommand<EDirection> JogStartCommand { get; }
    public IRelayCommand ApplySpeedCommand { get; }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ApplySpeed。
    /// 正文中的关键协作调用包括 TryParse、IsFinite、SetSpeed；具体条件和先后次序见方法体。
    /// </summary>
    private void ApplySpeed()
    {
        if (double.TryParse(SpeedInput, NumberStyles.Float, CultureInfo.CurrentCulture, out var velocity)
            && double.IsFinite(velocity) && velocity > 0)
            SetSpeed(velocity);
        else Model.LastMessage = "速度必须是大于零的有限数值。";
    }
}
