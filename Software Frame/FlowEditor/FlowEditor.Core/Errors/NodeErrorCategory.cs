// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeErrorCategory。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core;

/// <summary>
/// 节点错误类别（P0）。Runtime 不解析异常字符串，只按此类别做可重试判断、报警分类和 MES/Secs 对接。
/// UI 仍可显示中文消息，但调度/重试/遥测只依赖本枚举与 <see cref="FlowNodeException.ErrorCode"/>。
/// </summary>
public enum NodeErrorCategory
{
    /// <summary>未知/未分类（兼容旧节点直接抛出的异常）。</summary>
    Unknown = 0,

    /// <summary>配置错误：参数越界、缺输入、脚本引用缺失、模板缺失等。启动前 Validate 应优先发现。</summary>
    Configuration = 1,

    /// <summary>设备离线/连接失败：相机、PLC、扫码枪等未连接或连接被拒。</summary>
    DeviceOffline = 2,

    /// <summary>通信超时：读写超时、等待报文超时。默认可重试（由调用方决定边界）。</summary>
    Timeout = 3,

    /// <summary>流程取消：CancellationToken 触发。不计为失败，不重试。</summary>
    Cancelled = 4,

    /// <summary>视觉失败：定位失败、解码失败、模板匹配失败等业务性失败。默认不可重试。</summary>
    Vision = 5,

    /// <summary>通信协议失败：CRC 错误、协议 NAK、握手失败等。</summary>
    Communication = 6,

    /// <summary>脚本失败：编译错误与运行错误由 Script 插件进一步细分。</summary>
    Script = 7,

    /// <summary>资源不足/泄漏风险：Lease 获取失败、资源池耗尽、显式释放失败。</summary>
    Resource = 8,
}
