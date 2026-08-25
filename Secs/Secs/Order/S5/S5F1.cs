
using Extension.Core.DataEx;
using Prism.Ioc;
using Secs.Variable;
using Secs4Net;
using System.Collections.Generic;
using System.Linq;


namespace Secs.Order.S5
{

    /// <summary>
    /// 发送报警信息 E->H
    /// </summary>
    [SecsOrder("S5F1")]
    class S5F1 : SecsOrderBase
    {

        public override Item GetSendItem(params object[] pData)
        {
            var curalarm = (AlarmInfo)pData[0];

            List<Item> Items = new List<Item>();

            if (curalarm.Code == 1 && curalarm.Enable)
            {
                Items.Add(SetValue(curalarm));
            }

            else if (curalarm.Code == 0 && curalarm.Enable)
            {
                Items.Add(SetValue(curalarm));
            }


            if (Items.Count == 0) return null;
            Item SecsItem = Item.L(Items.ToArray());
            return SecsItem;
        }

        /// <summary>
        /// 设置报警信息格式
        /// </summary>
        /// <param name="alarmInfo"></param>
        /// <param name="IsSetBit">zhijiang</param>
        /// <returns></returns>
        public static Item SetValue(AlarmInfo alarmInfo, bool IsSetBit = true )
        {
            if (IsSetBit == false)
                return Item.L(
                   Item.B(alarmInfo.Code),
                   Item.A(alarmInfo.ID),
                   Item.A(alarmInfo.Description)
                    );
            else
                return Item.L(
                   Item.B((byte)(alarmInfo.Code << 7)),
                   Item.A(alarmInfo.ID),
                   Item.A(alarmInfo.Description)
                    );
        }
    }
}
