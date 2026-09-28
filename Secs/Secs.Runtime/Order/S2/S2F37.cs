using Prism.Ioc;
using Secs.Variable;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Secs.Order.S2
{ 
    /// <summary>  
  /// 开启或者关闭Event H->E  
  /// </summary>  
      [SecsOrder("S2F37")]
    class S2F37 : SecsOrderBase
    {
     
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                var data = SecsItemToList(pSecsMessage.SecsItem);
          
                bool isEnable = Convert.ToBoolean(data[0]);
                List<string> eventid = data.Skip(1).ToList();
 
                foreach (var item in eventid)
                {
                    CEIDInfo ceid = VIDManager.CEID.GetDatas().FirstOrDefault(x => x.ID == item);
                    if (ceid != null)
                    {
                        ceid.Enable = isEnable;
                    }
                }

                Item SecsItem = Item.B(0); //Success;  
                return SecsItem;
            }
            catch (Exception ex)
            {
                string message = ex.Message + ex.StackTrace;
                return null;  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }
    }
}
