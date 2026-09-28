using Prism.Ioc;
using Secs4Net;
using System;

namespace Secs.Order.S10
{
    /// <summary>
    /// 显示Host信息 H->E
    /// </summary>
    [SecsOrder("S10F3")]
    class S10F3:SecsOrderBase
    {

        public override Item GetReplyItem(SecsMessage pSecsMessage )
        {
            return S10TerminalMessageProcessor.Process("S10F3", pSecsMessage.SecsItem, SecsControl.TerminalDisplay);
        }
    }
}
