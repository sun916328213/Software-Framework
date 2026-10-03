// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：AxisStateModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;

namespace MotionControl.Controls.Models;

/// <summary>工作空间持有的单轴可观察状态；不读取或操作设备。</summary>
public sealed partial class AxisStateModel(int axis) : ObservableObject
{
    public int Axis { get; } = axis;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusSummary))] private bool _isAvailable;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusSummary))] private bool _isConnected;
    [ObservableProperty] private double? _commandPosition;
    [ObservableProperty] private double? _encoderPosition;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusSummary))] private bool _enabled;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusSummary))] private bool _moving;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusSummary))] private bool _inPosition;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusSummary))] private bool _alarm;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusSummary))] private bool _homed;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusSummary))] private bool _positiveLimit;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusSummary))] private bool _negativeLimit;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusSummary))] private bool _emergencyStop;
    [ObservableProperty] private string _lastMessage = string.Empty;
    [ObservableProperty] private string _speedInput = "10";

    public string StatusSummary
    {
        get
        {
            if (!IsAvailable) return "轴不可用";
            if (!IsConnected) return "未连接";
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
}
