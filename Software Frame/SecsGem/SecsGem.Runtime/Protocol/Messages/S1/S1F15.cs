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
using System.Threading.Tasks;


namespace SecsGem.Runtime.Order.S1
{
    /// <summary>
    /// OFLACK：下线应答码。GEM Control State Model §6.2 定义只有 0=OK。
    /// </summary>
    public enum EOfflineResult : byte
    {
        /// <summary>接受，设备转 OFFLINE。</summary>
        Ok = 0
    }

    /// <summary>
    /// 离线请求/离线请求确认 Offline H->E
    /// </summary>
    [SecsOrder("S1F15")]
    class S1F15 : SecsOrderBase
    {
        /// <summary>
        /// 收到 S1F15（Host 请求下线）：只回 OFLACK。
        /// 状态切换与 S6F11(Device Offline) 上报由 <c>ConnectManager.Offline_HtoE</c> 在
        /// 引擎触发 "S1F15" 事件后完成，保证 S1F16 先于 S6F11 出去。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage) => Item.B((byte)EOfflineResult.Ok);

        /// <summary>
        /// 获取发送数据。S1F15 是 Offline Request，按 E5 只能 Host→Equipment，
        /// 设备端不发起，返回 null 让引擎明确报"该指令不可发"而不是组个空包出去。
        /// </summary>
        public override Item GetSendItem(params object[] pData)
        {
            return null;
        }
    }
}
