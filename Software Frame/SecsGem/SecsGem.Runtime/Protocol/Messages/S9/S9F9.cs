// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SS9F9、S9F9。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Prism.Ioc、SecsGem.Runtime.Protocol.Building。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Prism.Ioc;
using SecsGem.Runtime.Protocol.Building;
using Secs4Net;

namespace SecsGem.Runtime.Order.S9
{
    /// <summary>
    /// 发送输入参数
    /// </summary>
    public struct SS9F9
    {
        /// <summary>
        /// SecsMessage
        /// </summary>
        public SecsMessage SecsMessage { get; set; }
        /// <summary>
        /// 设备ID
        /// </summary>
        public ushort DeviceID { get; set; }
        /// <summary>
        /// ID
        /// </summary>
        public int ID { get; set; }
    }

    /// <summary>
    /// 回复超时 T3超时 T3=45s E->H
    /// </summary>
    [SecsOrder("S9F9")]
    class S9F9 : SecsOrderBase
    {
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetSendItem。
        /// 正文中的关键协作调用包括 GetHeaderInfo、B；具体条件和先后次序见方法体。
        /// </summary>
        public override Item GetSendItem(params object[] pData)
        {
            SS9F9 data=(SS9F9)pData[0] ;
          
           byte[]  bytes = HsmsMessageHeaderBuilder.GetHeaderInfo(data.SecsMessage, data.DeviceID, data.ID);

            Item SecsItem = Item.B(bytes);
            return SecsItem;
        }
    }
}
