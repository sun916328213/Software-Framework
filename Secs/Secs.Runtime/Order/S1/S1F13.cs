using Secs4Net;
using Secs.Manager;
using System;
using Prism.Ioc;

namespace Secs.Order.S1
{
    /// <summary>
    /// 通信建立请求/通信建立请求确认 H<->E
    /// </summary>
     [SecsOrder("S1F13")]
    class S1F13 : SecsOrderBase
    {
      
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
       
        public override Item GetSendItem(params object[] pData  )
        {
            Item SecsItem = Item.L(
                Item.A(DeviceManager.EQName),
                Item.A(DeviceManager.SoftwareVersion));
            return SecsItem;
        }
    }
}
