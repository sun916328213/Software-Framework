using Prism.Ioc;
using Secs.Manager;
using Secs.Variable;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Secs.Order.S1
{
    /// <summary>
    /// 获取所选设备状态请求 获取VID SVID H->E
    /// </summary>
    [SecsOrder("S1F3")]
    class S1F3 : SecsOrderBase
    {
       
        public override Item  GetReplyItem(SecsMessage pSecsMessage   )
        {
            try
            {
                //解析数据
                var data = SecsItemToList(pSecsMessage.SecsItem);

                List<VIDInfo> _VID = VIDManager.VIDList.ToList();
 
                List<Item> Items = new List<Item>();

                //输出所有的VID
                if (data == null || data.Count == 0)
                {
                    foreach (var val in _VID)
                        Add(ref Items, val);

                }
                else
                {
                    var svid = data[0];
                    Dictionary<string, VIDInfo> vids = _VID.ToDictionary(s => s.ID, s => s);
                    foreach (var item in data)
                    {
                        if (vids.ContainsKey(item))
                            Add(ref Items, vids[item]);
                    }
                }

                Item SecsItem = Item.L(Items.ToArray());
                return SecsItem;
            }
            catch (Exception ex)
            {
                string message = ex.Message + ex.StackTrace;
                return null;  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }

        public void  Add(ref List<Item> Items,VIDInfo data)
        {
             var d1= AddData(data.Value, data.DataType);
            
                Items.Add(d1);
         
        }
    }
}
