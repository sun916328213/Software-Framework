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
    /// <remarks>
    /// 指令格式示例：Host 在线探测，无正文（不是 L[0]）；设备回型号和版本。
    /// <code><![CDATA[
    /// 'S1F1'W                         // Host→设备，W=1，请求应答
    /// .                              // 无正文，不是 L[0]
    /// 'S1F2'                          // 设备→Host，W=0
    /// <L [2]                          // 两项设备信息
    ///     <A [9] 'WORKBENCH'>          // MDLN：设备型号，取自配置
    ///     <A [3] '1.0'>                // SOFTREV：软件版本，取自配置
    /// >
    /// .
    /// ]]></code>
    /// MDLN/SOFTREV 来自设备配置；本例为示例值。当前 GetSendItem 返回 null，不支持从此组包入口主动发起。
    /// </remarks>
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
        /// 当前处理器未实现设备主动发送组包，返回 null；这是实现限制，不表示 S1F1 协议只允许 Host 发起。
        /// </summary>
        public override Item GetSendItem(params object[] pData)
        {
            return null;
        }
    }
}
