// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S1F13。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Secs4Net、SecsGem.Runtime.Manager。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Secs4Net;
using SecsGem.Runtime.Manager;
using System;
using Prism.Ioc;

namespace SecsGem.Runtime.Order.S1
{
    /// <summary>
    /// 通信建立请求/通信建立请求确认 H<->E
    /// </summary>
     [SecsOrder("S1F13")]
    class S1F13 : SecsOrderBase
    {
      
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、L、B、A；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage )
        {
            try
            {
                Item SecsItem;
                var data = SecsItemToList(pSecsMessage.SecsItem);
                Byte val = 0;
                if (data != null)
                    val = 0;
                else
                    val = 1;
                SecsItem = Item.L(
                    Item.B(val),
                          Item.L(
                            Item.A(DeviceManager.EQName),
                                  Item.A(DeviceManager.SoftwareVersion)
                          )
                    );

                return SecsItem;
            }
            catch (Exception ex)
            {
                string message = ex.Message + ex.StackTrace;

                ///解析失败 触发S9F7 未知错误
                return null;
            }
        }
       
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetSendItem。
        /// 正文中的关键协作调用包括 L、A；具体条件和先后次序见方法体。
        /// </summary>
        public override Item GetSendItem(params object[] pData  )
        {
            Item SecsItem = Item.L(
                Item.A(DeviceManager.EQName),
                Item.A(DeviceManager.SoftwareVersion));
            return SecsItem;
        }
    }
}
