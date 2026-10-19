// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S10F5。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Prism.Ioc、Secs4Net。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Prism.Ioc;
using Secs4Net;
using System;
using System.Collections.Generic;

namespace SecsGem.Runtime.Order.S10
{
    /// <summary>
    /// 显示Host信息 H->E
    /// </summary>
    /// <remarks>
    /// 指令格式示例：Host 向终端显示两行文本。
    /// <code><![CDATA[
    /// 'S10F5'W                        // Host→设备，多条终端显示
    /// <L [2]                          // TID、文本行列表
    ///     <B [1] 0x00>                // TID：终端标识，本例为 0
    ///     <L [2]                      // 两行文本
    ///         <A [5] 'Line1'>         // 第一行 TEXT
    ///         <A [5] 'Line2'>         // 第二行 TEXT
    ///     >
    /// >
    /// .
    /// 'S10F6'                         // 设备→Host，终端确认
    /// <B [1] 0x00>                    // ACKC10：0=成功入队（非已读），1=不接受
    /// .
    /// ]]></code>
    /// 上述 ASCII 行列表是常见示例，不是唯一类型；同样接受直接文本、数值、布尔和嵌套列表。
    /// 列表按 CRLF 保留顺序/空行，数值向量以空格分隔；TID 不混入正文。ACKC10=0 表示入队，1 为不接受。
    /// UI 异步显示，不等待操作员；格式非法走 S9F7，已入队后的显示失败记历史，不追发错误应答。
    /// </remarks>
    [SecsOrder("S10F5")]
    class S10F5:SecsOrderBase
    {
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 Process；具体条件和先后次序见方法体。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage  )
        {
            return S10TerminalMessageProcessor.Process("S10F5", pSecsMessage.SecsItem, SecsGemRuntime);
        }
    }
}
