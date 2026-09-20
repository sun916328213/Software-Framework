using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using Prism.Ioc;
using Secs4Net;
using System;


namespace Secs.Order.S1
{
    /// <summary>
    /// 在线结果
    /// </summary>
    public enum EOnflineResult
    {
        OnlineSuccess,
        OnlineFail,
        AlreadyOnline
    }

    /// <summary>
    /// 在线请求/在线请求确认 Online H->E
    /// </summary>
    [SecsOrder("S1F17")]
    class S1F17 : SecsOrderBase
    {
     
        public override Item GetReplyItem(SecsMessage pSecsMessage )
        {
            try
            {
                Item SecsItem;
                if ( SecsControl.GetDeviceMode() != ESecsMode.Online_Remote)
                {
                    if ( SecsControl.GetDeviceStatus() != EDeviceStatus.Running)
                    {
                        SecsItem = Item.B((byte)EOnflineResult.OnlineSuccess); //Success
                        return SecsItem;
                    }
                    else
                    {
                        SecsItem = Item.B((byte)EOnflineResult.OnlineFail); //Success
                        return SecsItem;
                    }
                }
                else
                {
                    SecsItem = Item.B((byte)EOnflineResult.OnlineFail); //Success
                    return SecsItem;
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message + ex.StackTrace;
                return null;
            }
        }

        /// <summary>
        /// 获取发送数据
        /// </summary>
        public override Item GetSendItem(params object[] pData)
        {
            return null;
        }
    }
}
