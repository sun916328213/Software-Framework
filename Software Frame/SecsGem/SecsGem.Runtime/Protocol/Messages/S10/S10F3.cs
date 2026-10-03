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
