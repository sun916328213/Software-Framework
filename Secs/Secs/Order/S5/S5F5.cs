using Extension.Core.DataEx;
using Prism.Ioc;
using Secs.Variable;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Secs.Order.S5
{
    /// <summary>
    /// 报警信息List H->E
    /// </summary>
    [SecsOrder("S5F5")]
    class S5F5 : SecsOrderBase
    {
     

        public override Item GetReplyItem(SecsMessage pSecsMessage )
        {
            try
            {
                var data = SecsItemToList(pSecsMessage.SecsItem);
                Alarm alarm = VIDManager.Alarm;
 
               var  _alarmcopy = alarm.GetDatas();

                List<Item> Items = new List<Item>();

                if (data == null || data.Count == 0)
                {
                    foreach (AlarmInfo curalarm in _alarmcopy)
                    {
                        Items.Add(S5F1.SetValue(curalarm));
                    }
                }
                else
                {
                    var _alarmcopybyid = _alarmcopy.ToDictionary(x => x.ID, x => x);
                    string ids = data[0];
                    string[] idarray = ids.Split(',');
                    foreach (var id in idarray)
                    {
                        if (_alarmcopybyid.TryGetValue(id, out var curalarm))
                            Items.Add(S5F1.SetValue(curalarm));
                    }
                }

                if (Items.Count == 0) return null;
                Item SecsItem = Item.L(Items.ToArray()); ;
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
