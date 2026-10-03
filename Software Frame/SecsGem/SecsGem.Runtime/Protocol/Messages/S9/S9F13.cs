// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SS9F13、S9F13。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Prism.Ioc、Secs4Net。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Prism.Ioc;
using Secs4Net;

namespace SecsGem.Runtime.Order.S9
{
    /// <summary>
    /// 发送输入数据结构体
    /// </summary>
    public struct SS9F13
    {
        /// <summary>
        /// SxFx指令
        /// </summary>
        public string Order { get; set; }
        /// <summary>
        /// 超时信息
        /// </summary>
        public string Message { get; set; }
    }
    /// <summary>
    /// 对话流程超时 E->H
    /// </summary>
    [SecsOrder("S9F13")]
    class S9F13 : SecsOrderBase
    {
     
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetSendItem。
        /// 正文中的关键协作调用包括 L、A、ToString；具体条件和先后次序见方法体。
        /// </summary>
        public override Item GetSendItem(params object[] pData )
        {
            SS9F13 data = (SS9F13)pData[0];
            Item SecsItem= Item.L(
               Item.A(data.Order.ToString()),
                    Item.A(data.Message.ToString())
                );
            return SecsItem;
        }
    }
}
