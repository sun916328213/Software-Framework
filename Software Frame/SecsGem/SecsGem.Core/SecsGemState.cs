// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsGemMode、SecsGemEquipmentStatus、SecsGemState。成员说明可结合下方具体实现阅读。
namespace SecsGem.Core;

/// <summary>
/// SecsGemMode 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// </summary>
public enum SecsGemMode
{
    Offline,
    OnlineLocal,
    OnlineRemote
}

/// <summary>
/// SecsGemEquipmentStatus 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// </summary>
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
