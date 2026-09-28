using Prism.Ioc;
using Secs.Manager;
using Secs.Variable;
using Secs4Net;
using System;
using System.Collections.Generic;

namespace Secs.Order.S1
{
    /// <summary>
    /// 状态变量名称列表请求 H->E
    /// </summary>
    [SecsOrder("S1F11")]
    class S1F11 : SecsOrderBase
    {
       
        public override Item GetReplyItem(SecsMessage pSecsMessage  )
        {
            try
            {
                //解析数据
                List<string> data = SecsItemToList(pSecsMessage.SecsItem);
 
                List<Item> Items = new List<Item>();

                if (data == null || data.Count == 0)
                {
                    foreach (var val in VIDManager.VIDList)
                        Items.Add(SetItem(val));
                }
                else
                {
                    var svid = data[0];

                    foreach (var item in data)
                    {
                        var vids = VIDManager.GetVIDByID(item);
                        if (vids != null)
                            Items.Add(SetItem(vids));
                    }
                }

                Item SecsItem = Item.L(Items);
                return SecsItem;
            }
            catch (Exception ex)
            {
                string message = ex.Message + ex.StackTrace;
                return null;    
            }
        }

        /// <summary>
        /// 將VID转化成Secs数据
        /// </summary>
        /// <param name="pCVID"></param>
        /// <returns></returns>
        private Item  SetItem(VIDInfo pCVID)
        {
            List<Item> Items = new List<Item>();
            Items.Add(Item.A(pCVID.ID));
            Items.Add(Item.A(pCVID.Name));
            Items.Add(Item.A(pCVID.DataTypeName));
            return Item.L(Items.ToArray());
        }
    }
}
