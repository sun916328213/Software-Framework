// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：EOfflineResult、S1F15。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Extension.Core.Models、Extension.Imaging.Models。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using Prism.Ioc;
using SecsGem.Runtime.Manager;
using Secs4Net;
using System;


namespace SecsGem.Runtime.Order.S1
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
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 B；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetSendItem。
        /// </summary>
        public override Item GetSendItem(params object[] pData)
        { 
            return null;
        }
    }
}
