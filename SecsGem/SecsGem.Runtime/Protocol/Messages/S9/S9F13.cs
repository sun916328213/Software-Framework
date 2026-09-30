using Prism.Ioc;
using Secs4Net;

namespace Secs.Order.S9
{
    /// <summary>
    /// 发送输入数据结构体
    /// </summary>
    public struct SS9F13
    {
        /// <summary>
        /// SxFx指令
        /// </summary>
        public string Order { get; set; }
        /// <summary>
        /// 超时信息
        /// </summary>
        public string Message { get; set; }
    }
    /// <summary>
    /// 对话流程超时 E->H
    /// </summary>
    [SecsOrder("S9F13")]
    class S9F13 : SecsOrderBase
    {
     
        public override Item GetSendItem(params object[] pData )
        {
            SS9F13 data = (SS9F13)pData[0];
            Item SecsItem= Item.L(
               Item.A(data.Order.ToString()),
                    Item.A(data.Message.ToString())
                );
            return SecsItem;
        }
    }
}
