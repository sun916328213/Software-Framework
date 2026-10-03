// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsStateMachine。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Runtime、Extension.Automation.Device、Extension.Automation.Secs。导入不代表所有类型都会在运行时加载。
using SecsGem.Runtime;
using Extension.Automation.Device;
using Extension.Automation.Secs;

namespace SecsGem.Runtime.StateMachine;

/// <summary>Tracks equipment state and whether a change has been reported to the host.</summary>
/// <remarks>
/// SecsStateMachine 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// </remarks>
internal sealed class SecsStateMachine
{
    private readonly object _gate = new();
    private EDeviceStatus _status = EDeviceStatus.Idle;
    private ESecsMode _mode = ESecsMode.Offline;
    private EDeviceStatus _reportedStatus;
    private ESecsMode _reportedMode;
    private bool _connected;

    public EDeviceStatus Status { get { lock (_gate) return _status; } }
    public ESecsMode Mode { get { lock (_gate) return _mode; } }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetConnection。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
    public void SetConnection(bool connected)
    {
        lock (_gate) _connected = connected;
    }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetMode。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
    public bool SetMode(ESecsMode mode)
    {
        lock (_gate)
        {
            _mode = mode;
            return _connected && _mode != _reportedMode;
        }
    }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetStatus。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
    public bool SetStatus(EDeviceStatus status)
    {
        lock (_gate)
        {
            _status = status;
            return _connected && _status != _reportedStatus;
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 MarkModeReported。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
    public void MarkModeReported()
    {
        lock (_gate) _reportedMode = _mode;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 MarkStatusReported。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
    public void MarkStatusReported()
    {
        lock (_gate) _reportedStatus = _status;
    }
}
