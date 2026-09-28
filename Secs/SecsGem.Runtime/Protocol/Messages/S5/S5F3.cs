using Prism.Ioc;
using Secs.Variable;
using Secs4Net;
using System;
using System.Linq;

namespace Secs.Order.S5
{
    /// <summary>
    /// 启用报警信息 H->E
    /// </summary>
    [SecsOrder("S5F3")]
    class S5F3 : SecsOrderBase
    {
     

        public override Item GetReplyItem(SecsMessage pSecsMessage   )
        {
            Item SecsItem;
            try
            {
                //解析数据
                var data = SecsItemToList(pSecsMessage.SecsItem);
 
                //格式
                bool enable = data[0] == "1"?true:false ;
                string ids = data[1];

                Alarm alarm = VIDManager. Alarm;

                if (ids.Length == 0)
                {
                    alarm.GetDatas().Select(s => s.Enable = enable);
                }
                else if (ids.Contains(","))  ////多个报警信息
                {
                    var idarray = ids.Split(',');
                    //遍历_alarmcopy 将匹配的报警信息启用
                    foreach (var id in idarray)
                    {
                        alarm.SetEnable(id, enable);
                    }
                }
                else  //单个报警信息
                {
                    alarm.SetEnable(ids, enable);
                }
                alarm.Save();
                SecsItem = Item.B(0);//succsee
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
