using Prism.Ioc;
using Secs.Manager;
using Secs4Net;

namespace Secs.Order.S1
{
    /// <summary>
    /// 在线确认请求/在线数据 H<->E
    /// </summary>
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
        /// 获取发送数据
        /// </summary>
        /// <param name="pData"></param>
        /// <returns></returns>
        public override Item GetSendItem(params object[] pData)
        {
            return null;
        }
    }
}
