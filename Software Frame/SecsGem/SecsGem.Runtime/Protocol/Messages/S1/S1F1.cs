// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S1F1。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Prism.Ioc、SecsGem.Runtime.Manager。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Prism.Ioc;
using SecsGem.Runtime.Manager;
using Secs4Net;

namespace SecsGem.Runtime.Order.S1
{
    /// <summary>
    /// 在线确认请求/在线数据 H<->E
    /// </summary>
    [SecsOrder("S1F1")]
    class S1F1:SecsOrderBase
    {

        /// <summary>
        /// 获取回复数据
        /// </summary>
        /// <param name="pSecsMessage"></param>
        /// <returns></returns>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            Item SecsItem = Item.L(
                 Item.A(DeviceManager.EQName),
                 Item.A(DeviceManager.SoftwareVersion));
            return SecsItem;
        }
        

        /// <summary>
        /// 获取发送数据
        /// </summary>
        /// <param name="pData"></param>
        /// <returns></returns>
        public override Item GetSendItem(params object[] pData)
        {
            return null;
        }
    }
}
