using Prism.Ioc;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Secs.Order.S2
{
    /// <summary>  
    /// 修改ReportID中的VID H->E  
    /// </summary>  
      [SecsOrder("S2F35")]
    class S2F35 : SecsOrderBase
    {
     

        public override Item GetReplyItem(SecsMessage pSecsMessage )
        {
            try
            {
                var data = SecsItemToList(pSecsMessage.SecsItem);
           
                //解析数据
                string dataid = data[0];
                string eventid = data[1];
                List<string> reportid = data.Skip(2).ToList();
 
                //修改ReportID挂载数据
                VIDManager. ReportID.Modify(eventid, reportid);

                //刷新数据
                VIDManager. ReportID.Update(VIDManager.VIDList.ToList()); //刷新ReportID数据
                VIDManager. CEID.Update(VIDManager. ReportID.GetDatas());//刷新CEID数据

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
