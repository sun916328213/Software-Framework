using Extension.Automation.Device;
using Extension.Automation.Secs;

namespace SecsGem.Runtime.StateMachine;

/// <summary>Tracks equipment state and whether a change has been reported to the host.</summary>
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

    public void SetConnection(bool connected)
    {
        lock (_gate) _connected = connected;
    }

    public bool SetMode(ESecsMode mode)
    {
        lock (_gate)
        {
            _mode = mode;
            return _connected && _mode != _reportedMode;
        }
    }

    public bool SetStatus(EDeviceStatus status)
    {
        lock (_gate)
        {
            _status = status;
            return _connected && _status != _reportedStatus;
        }
    }

    public void MarkModeReported()
    {
        lock (_gate) _reportedMode = _mode;
    }

    public void MarkStatusReported()
    {
        lock (_gate) _reportedStatus = _status;
    }
}
