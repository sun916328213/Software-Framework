using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using Prism.Ioc;
using Secs.Manager;
using Secs4Net;
using System;


namespace Secs.Order.S1
{
    /// <summary>
    /// 离线结果
    /// </summary>
    public enum EOfflineResult
    {
        OfflineSuccess=0,
        OfflineFail=1,
        AlreadyOffline=2
    }

    /// <summary>
    /// 离线请求/离线请求确认 Offline H->E
    /// </summary>
    [SecsOrder("S1F15")]
    class S1F15 : SecsOrderBase
    {
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                Item SecsItem;
                if (StatusManager.CurrentMode != ESecsMode.Online_Local)
                {
                    if (StatusManager.CurrentStatus != EDeviceStatus. Running)
                    {
                        SecsItem = Item.B((byte)EOfflineResult.OfflineSuccess); //Success
                        return SecsItem;
                    }
                    else
                    {
                        SecsItem = Item.B((byte)EOfflineResult.OfflineFail);//Fail
                        return SecsItem;
                    }
                }
                else
                {
                    SecsItem = Item.B((byte)EOfflineResult.AlreadyOffline);//Fail
                    return SecsItem;
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message + ex.StackTrace;
                return null;
            }
        }

        public override Item GetSendItem(params object[] pData)
        { 
            return null;
        }
    }
}
