// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：ITerminalDisplay。成员说明可结合下方具体实现阅读。
namespace SecsGem.Core;

/// <summary>向设备操作界面显示 Host 发送的终端消息。</summary>
public interface ITerminalDisplay
{
    /// <summary>接收展示请求，不应等待操作员关闭窗口；协议 ACK 不表示已阅读。</summary>
    void Show(string title, string message);

    /// <summary>带终端 ID 和通知编号的展示入口；旧实现仍通过字符串入口兼容。</summary>
    void Show(SecsTerminalMessage message) => Show(message.Command, message.Text);
}
