using Prism.Ioc;
using Secs.Tool;
using Secs4Net;

namespace Secs.Order.S9
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
        public override Item GetSendItem(params object[] pData)
        {
            SS9F9 data=(SS9F9)pData[0] ;
          
           byte[]  bytes = MessageHeader_Refactor.GetHeaderInfo(data.SecsMessage, data.DeviceID, data.ID);

            Item SecsItem = Item.B(bytes);
            return SecsItem;
        }
    }
}
