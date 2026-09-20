using Extension.UI.Controls;
using Prism.Ioc;
using Secs.UI;
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
            try
            {
                //解析数据
                List<string> data = SecsItemToList(pSecsMessage.SecsItem);
                var showdata = string.Join("/r/n", data);

                //显示数据
                if (data != null)
                {
                    Action action = new Action(() =>
                    {
                        MessagePrompt messagePrompt = new MessagePrompt("S10F5", showdata);
                        messagePrompt.ShowDialog();
                    });
                    action.UIInvoke();
                }
                Item SecsItem = Item.B(0);
                return SecsItem;
            }
            catch 
            {
                return null;
            }
        }
    }
}
