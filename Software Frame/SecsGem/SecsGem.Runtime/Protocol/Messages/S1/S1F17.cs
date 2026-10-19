// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：EOnflineResult、S1F17。成员说明可结合下方具体实现阅读。
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
using Secs4Net;
using System;
using System.Threading.Tasks;


namespace SecsGem.Runtime.Order.S1
{
    /// <summary>
    /// ONLACK：上线应答码。取值必须与 GEM Control State Model §6.4 一致——
    /// 1 表示"已在线"而不是"失败"，2 才是"设备状态不允许"。
    /// 原枚举把 1/2 写成 OnlineFail/AlreadyOnline，语义整个错位。
    /// </summary>
    public enum EOnflineResult : byte
    {
        /// <summary>接受，设备转 ON-LINE REMOTE。</summary>
        Accepted = 0,
        /// <summary>不接受：设备已经在线。</summary>
        AlreadyOnline = 1,
        /// <summary>不接受：设备状态不允许上线（联锁、维护中）。</summary>
        NotReady = 2
    }

    /// <summary>
    /// 在线请求/在线请求确认 Online H->E
    /// </summary>
    /// <remarks>
    /// 指令格式示例：Host 请求上线，无正文；下面示例为接受。
    /// <code><![CDATA[
    /// 'S1F17'W                        // Host→设备，请求上线
    /// .                              // 无正文
    /// 'S1F18'                         // 设备→Host，上线应答
    /// <B [1] 0x00>                    // ONLACK：0=接受，1=已在线，2=不允许
    /// .
    /// ]]></code>
    /// ONLACK：0=接受、1=已在线、2=当前不允许；只有接受后才由 ConnectManager 切换状态。
    /// </remarks>
    [SecsOrder("S1F17")]
    class S1F17 : SecsOrderBase
    {
     
        /// <summary>
        /// 收到 S1F17（Host 请求上线）：只判定并回 ONLACK，不在这里切状态。
        /// <para>
        /// 收发分离：引擎回完 S1F18 后会 <c>EventHelper.TriggerEvent("S1F17")</c>，
        /// 由 <c>ConnectManager.Online_HtoE</c> 切控制状态并上报 S6F11(Device Online)。
        /// 这样 S1F18 一定先于 S6F11 出去（§10.1 的顺序要求），且状态切换点只有一处。
        /// </para>
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            if (SecsGemRuntime.GetDeviceMode() != ESecsMode.Offline)
                return Item.B((byte)EOnflineResult.AlreadyOnline);

            // 设备运行中不允许被 Host 拉上线
            if (SecsGemRuntime.GetDeviceStatus() == EDeviceStatus.Running)
                return Item.B((byte)EOnflineResult.NotReady);

            return Item.B((byte)EOnflineResult.Accepted);
        }

        /// <summary>
        /// 获取发送数据。S1F17 是 Online Request，按 E5 只能 Host→Equipment，
        /// 设备端不发起，返回 null 让引擎明确报"该指令不可发"而不是组个空包出去。
        /// </summary>
        public override Item GetSendItem(params object[] pData)
        {
            return null;
        }
    }
}
