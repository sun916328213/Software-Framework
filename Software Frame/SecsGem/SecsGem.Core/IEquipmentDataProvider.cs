// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：IEquipmentDataProvider。成员说明可结合下方具体实现阅读。
namespace SecsGem.Core;

/// <summary>由设备宿主提供当前变量值；键为已配置的 VID 名称。</summary>
public interface IEquipmentDataProvider
{
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetVariableValues。
    /// </summary>
    IReadOnlyDictionary<string, object?> GetVariableValues();
}
