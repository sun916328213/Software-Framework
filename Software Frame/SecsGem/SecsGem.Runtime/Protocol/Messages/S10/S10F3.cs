// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S10F3。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Prism.Ioc、Secs4Net。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Prism.Ioc;
using Secs4Net;
using System;

namespace SecsGem.Runtime.Order.S10
{
    /// <summary>
    /// 显示Host信息 H->E
    /// </summary>
    /// <remarks>
    /// 指令格式示例：Host 向终端显示一条文本。
    /// <code><![CDATA[
    /// 'S10F3'W                        // Host→设备，单条终端显示
    /// <L [2]                          // TID、TEXT
    ///     <B [1] 0x00>                // TID：终端标识，本例为 0
    ///     <A [5] 'Hello'>             // TEXT：显示文本，ASCII 字节数为 5
    /// >
    /// .
    /// 'S10F4'                         // 设备→Host，终端确认
    /// <B [1] 0x00>                    // ACKC10：当前处理成功为 0
    /// .
    /// ]]></code>
    /// 当前处理器统一平铺全部叶子项交给 TerminalDisplay，包含 TID；不是仅抽取 TEXT。解析/显示失败返回 null，走 S9F7。
    /// </remarks>
    [SecsOrder("S10F3")]
    class S10F3:SecsOrderBase
    {

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 Process；具体条件和先后次序见方法体。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage )
        {
            return S10TerminalMessageProcessor.Process("S10F3", pSecsMessage.SecsItem, SecsGemRuntime.TerminalDisplay);
        }
    }
}
