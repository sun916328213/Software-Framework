// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：MotionPageModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.ObjectModel、CommunityToolkit.Mvvm.ComponentModel、MotionControl.Core.Abstractions、MotionControl.Core.Definitions。导入不代表所有类型都会在运行时加载。
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;

namespace MotionControl.Controls.Models;

/// <summary>运动页面的配置、选择和运行状态。</summary>
public sealed partial class MotionPageModel : ObservableObject
{
    public EMotionCardType[] Vendors { get; } = Enum.GetValues<EMotionCardType>();
    public ObservableCollection<AxisStateModel> Axes { get; } = [];
    [ObservableProperty] private EMotionCardType _selectedVendor = EMotionCardType.Simulation;
    [ObservableProperty] private int _axisCount = 2;
    [ObservableProperty] private string _ipAddress = "127.0.0.1";
    [ObservableProperty] private IMotionCard? _selectedCard;
    [ObservableProperty] private int[] _axisIndices = [];
    [ObservableProperty] private int _selectedAxisIndex;
    [ObservableProperty] private string _statusMessage = "未连接";
    [ObservableProperty] private bool _isConnected;
}
