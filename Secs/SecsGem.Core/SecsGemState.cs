namespace SecsGem.Core;

public enum SecsGemMode
{
    Offline,
    OnlineLocal,
    OnlineRemote
}

public enum SecsGemEquipmentStatus
{
    Idle,
    Ready,
    Running,
    Paused,
    Error
}

/// <summary>运行时状态的当前快照。</summary>
public readonly record struct SecsGemState(
    bool IsConnected,
    SecsGemMode Mode,
    SecsGemEquipmentStatus EquipmentStatus);
