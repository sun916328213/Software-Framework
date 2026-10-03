// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：MotionWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Controls.Models、MotionControl.Core.Abstractions、MotionControl.Core.Definitions、MotionControl.Runtime.Services。导入不代表所有类型都会在运行时加载。
using MotionControl.Controls.Models;
using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Services;

namespace MotionControl.Controls.Services;

/// <summary>运动页面流程与自有设备生命周期；不替换或释放宿主的其他控制卡。</summary>
public sealed class MotionWorkspace : IDisposable
{
    private readonly MotionManager _manager;
    private readonly bool _ownsManager;
    private IMotionCard? _ownedCard;
    private bool _disposed;

    /// <summary>
    /// 初始化 MotionWorkspace 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public MotionWorkspace() : this(new MotionManager(), true) { }
    /// <summary>
    /// 初始化 MotionWorkspace 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public MotionWorkspace(MotionManager manager) : this(manager, false) { }
    /// <summary>
    /// 初始化 MotionWorkspace 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
    /// </summary>
    private MotionWorkspace(MotionManager manager, bool ownsManager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _ownsManager = ownsManager;
    }

    public MotionPageModel Model { get; } = new();

    /// <summary>
    /// 发起连接操作；对应入口为 Connect。
    /// 正文中的关键协作调用包括 ThrowIf、ReleaseCard、Add、Create、ConnectResult；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public void Connect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Model.AxisCount is < 1 or > 16) { Model.StatusMessage = "轴数必须在 1 到 16 之间。"; return; }
        try
        {
            ReleaseCard();
            var parameter = new MotionCardParam
            {
                Name = "MotionWorkspace", CardType = Model.SelectedVendor, IpAddress = Model.IpAddress,
            };
            for (var i = 0; i < Model.AxisCount; i++) parameter.Axises.Add(new AxisParam { Name = $"Axis{i}" });
            _ownedCard = _manager.Create(parameter);
            var result = _ownedCard.ConnectResult();
            if (!result.IsSuccess)
            {
                Model.StatusMessage = $"连接失败：{result.Message ?? result.Code.ToString()}";
                ReleaseCard();
                return;
            }
            Model.SelectedCard = _ownedCard;
            Model.AxisIndices = Enumerable.Range(0, _ownedCard.AxisCount).ToArray();
            Model.SelectedAxisIndex = 0;
            foreach (var axis in Model.AxisIndices)
            {
                var workspace = AxisWorkspace.For(_ownedCard, axis);
                workspace.Refresh();
                Model.Axes.Add(workspace.Model);
            }
            Model.IsConnected = true;
            Model.StatusMessage = $"已连接：{_ownedCard.Name}（{Model.SelectedVendor}，{_ownedCard.AxisCount} 轴）。";
        }
        catch (Exception ex) { Model.StatusMessage = $"连接异常：{ex.Message}"; }
    }

    /// <summary>
    /// 执行断开连接操作；对应入口为 Disconnect。
    /// 正文中的关键协作调用包括 ThrowIf、ReleaseCard；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public void Disconnect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try { ReleaseCard(); Model.StatusMessage = "已断开"; }
        catch (Exception ex) { Model.StatusMessage = $"断开失败：{ex.Message}"; }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReleaseCard。
    /// 正文中的关键协作调用包括 Remove、Refresh、For、Clear；具体条件和先后次序见方法体。
    /// </summary>
    private void ReleaseCard()
    {
        if (_ownedCard is not null)
        {
            // Remove 保留断开失败的卡，避免仍运行的设备失去所有者。
            _manager.Remove(_ownedCard);
            foreach (var axis in Model.Axes) AxisWorkspace.For(_ownedCard, axis.Axis).Refresh();
            _ownedCard = null;
        }
        Model.SelectedCard = null;
        Model.AxisIndices = [];
        Model.Axes.Clear();
        Model.IsConnected = false;
    }

    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 ReleaseCard、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        ReleaseCard();
        if (_ownsManager) _manager.Dispose();
        _disposed = true;
    }
}
