// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：WpfTerminalDisplay。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Controls、SecsGem.Core、SecsGem.Runtime、System.Windows。导入不代表所有类型都会在运行时加载。
using SecsGem.Controls;
using SecsGem.Core;
using SecsGem.Runtime;
using System.Windows;

namespace SecsGem.Controls.Terminal;

/// <summary>通过现有 WPF 提示窗口显示 S10 终端消息。</summary>
public sealed class WpfTerminalDisplay : ITerminalDisplay
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Show。
    /// 正文中的关键协作调用包括 Invoke、ShowDialog；具体条件和先后次序见方法体。
    /// </summary>
    public void Show(string title, string message)
    {
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("S10 terminal display requires a WPF application.");
        dispatcher.Invoke(() => new MessagePrompt(title, message).ShowDialog());
    }
}
