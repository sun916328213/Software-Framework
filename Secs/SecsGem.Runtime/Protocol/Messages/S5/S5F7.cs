using Prism.Ioc;
using Secs.Variable;
using Secs4Net;
using System.Collections.Generic;

namespace Secs.Order.S5
{
    /// <summary>
    /// Enable报警信息集合 H->E
    /// </summary>
    [SecsOrder("S5F7")]
    class S5F7 : SecsOrderBase
    {
     

        public override Item GetReplyItem(SecsMessage pSecsMessage  )
        {
            Alarm alarm = VIDManager.Alarm;

            List<AlarmInfo> _alarmcopy = alarm.GetDatas();
           
            List<Item> Items = new List<Item>();
            if (_alarmcopy != null) 
            {

                foreach (AlarmInfo curalarm in _alarmcopy)
                {
                    if (curalarm.Enable)
                    {
                        Items.Add(S5F1.SetValue(curalarm));
                    }
                }

                if (Items.Count == 0) return null;
                Item SecsItem = Item.L(Items.ToArray()); ;
                return SecsItem;
            }
            else
                return null;
        }
    }
}
