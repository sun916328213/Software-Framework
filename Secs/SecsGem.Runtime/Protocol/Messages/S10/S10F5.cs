using Prism.Ioc;
using Secs4Net;
using System;
using System.Collections.Generic;

namespace Secs.Order.S10
{
    /// <summary>
    /// 显示Host信息 H->E
    /// </summary>
    [SecsOrder("S10F5")]
    class S10F5:SecsOrderBase
    {
        public override Item GetReplyItem(SecsMessage pSecsMessage  )
        {
            return S10TerminalMessageProcessor.Process("S10F5", pSecsMessage.SecsItem, SecsControl.TerminalDisplay);
        }
    }
}
