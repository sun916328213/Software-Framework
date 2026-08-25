using Extension.Core.ControlEx;
using Prism.Ioc;
using Secs.UI;
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
            try
            {
                //解析数据
                var data = SecsItemToList(pSecsMessage.SecsItem);
                var showdata = string.Join("/r/n",data);
 
                //显示数据
                if (data != null)
                {
                    Action action = new Action(() =>
                    {
                       MessagePrompt messagePrompt = new MessagePrompt("S10F3", showdata);
                        messagePrompt.ShowDialog();
                    });
                    action.UIInvoke();
                }

                Item SecsItem = Item.B(0);
                return SecsItem;
            }
            catch (Exception ex)
            {
                string message = ex.Message + ex.StackTrace;
                return null;  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 }
            }
        }
    }
}
