// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S5F1。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Extension.Core.Buffers、Extension.IO.Compression。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;

using Extension.Core.Buffers;
using Extension.IO.Compression;
using Prism.Ioc;
using SecsGem.Runtime.Variable;
using Secs4Net;
using System.Collections.Generic;
using System.Linq;


namespace SecsGem.Runtime.Order.S5
{

    /// <summary>
    /// 发送报警信息 E->H
    /// </summary>
    [SecsOrder("S5F1")]
    class S5F1 : SecsOrderBase
    {

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetSendItem。
        /// 正文中的关键协作调用包括 Add、SetValue、L、ToArray；具体条件和先后次序见方法体。
        /// </summary>
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
