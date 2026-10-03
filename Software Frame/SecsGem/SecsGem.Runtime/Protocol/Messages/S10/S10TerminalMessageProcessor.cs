// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S10TerminalMessageProcessor。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Secs4Net、SecsGem.Runtime.Protocol.Parsing。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Secs4Net;
using SecsGem.Runtime.Protocol.Parsing;

namespace SecsGem.Runtime.Order.S10;

/// <summary>处理 S10F3/S10F5 终端消息，并生成终端确认应答。</summary>
internal static class S10TerminalMessageProcessor
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Process。
    /// 正文中的关键协作调用包括 Flatten、Join、Show、B；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public static Item? Process(string command, Item? terminalText, ITerminalDisplay display)
    {
        try
        {
            if (terminalText is not null)
            {
                var lines = SecsItemParser.Flatten(terminalText);
                var message = string.Join("/r/n", lines);
                if (lines is not null)
                    display.Show(command, message);
            }

            return Item.B(0);
        }
        catch
        {
            // 保留原有协议行为：解析或显示失败交给调用方触发 S9F7。
            return null;
        }
    }
}
