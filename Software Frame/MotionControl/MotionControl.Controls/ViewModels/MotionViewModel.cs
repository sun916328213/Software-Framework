// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：MotionViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.ComponentModel、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、MotionControl.Controls.Models、MotionControl.Controls.Services、MotionControl.Core.Abstractions。导入不代表所有类型都会在运行时加载。
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotionControl.Controls.Models;
using MotionControl.Controls.Services;
using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;

namespace MotionControl.Controls.ViewModels;

/// <summary>运动页面交互；Model 和设备生命周期归 Workspace。</summary>
public partial class MotionViewModel : ObservableObject, IDisposable
{
    private readonly bool _ownsWorkspace;
    /// <summary>
    /// 初始化 MotionViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public MotionViewModel(MotionWorkspace workspace) : this(workspace, false) { }
    /// <summary>
    /// 初始化 MotionViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof、AddHandler；具体条件和先后次序见方法体。
    /// </summary>
    protected MotionViewModel(MotionWorkspace workspace, bool ownsWorkspace)
    {
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _ownsWorkspace = ownsWorkspace;
        PropertyChangedEventManager.AddHandler(Model, OnModelChanged, string.Empty);
    }

    public MotionWorkspace Workspace { get; }
    public MotionPageModel Model => Workspace.Model;
    // 原工作台公开成员保留为兼容转发；Controls 页面绑定 Model。
    public EMotionCardType[] Vendors => Model.Vendors;
    public EMotionCardType SelectedVendor { get => Model.SelectedVendor; set => Model.SelectedVendor = value; }
    public int AxisCount { get => Model.AxisCount; set => Model.AxisCount = value; }
    public string IpAddress { get => Model.IpAddress; set => Model.IpAddress = value ?? string.Empty; }
    public IMotionCard? SelectedCard => Model.SelectedCard;
    public int[] AxisIndices => Model.AxisIndices;
    public int SelectedAxisIndex { get => Model.SelectedAxisIndex; set => Model.SelectedAxisIndex = value; }
    public string StatusMessage => Model.StatusMessage;
    public bool IsConnected => Model.IsConnected;
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnModelChanged。
    /// 正文中的关键协作调用包括 OnPropertyChanged；具体条件和先后次序见方法体。
    /// </summary>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(e.PropertyName);

    /// <summary>
    /// 发起连接操作；对应入口为 Connect。
    /// 正文中的关键协作调用包括 Connect；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand] public void Connect() => Workspace.Connect();
    /// <summary>
    /// 执行断开连接操作；对应入口为 Disconnect。
    /// 正文中的关键协作调用包括 Disconnect；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand] public void Disconnect() => Workspace.Disconnect();
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 RemoveHandler、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        PropertyChangedEventManager.RemoveHandler(Model, OnModelChanged, string.Empty);
        if (_ownsWorkspace) Workspace.Dispose();
    }
}
