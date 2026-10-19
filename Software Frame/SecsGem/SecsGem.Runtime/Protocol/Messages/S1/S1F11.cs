// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S1F11。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Prism.Ioc、SecsGem.Runtime.Manager。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Prism.Ioc;
using SecsGem.Runtime.Manager;
using SecsGem.Runtime.Variable;
using Secs4Net;
using System;
using System.Collections.Generic;

namespace SecsGem.Runtime.Order.S1
{
    /// <summary>
    /// 状态变量名称列表请求 H->E
    /// </summary>
    /// <remarks>
    /// 指令格式示例：查询 VID=1001 的说明。
    /// <code><![CDATA[
    /// 'S1F11'W                        // Host→设备，请求变量名录
    /// <L [1]                          // 请求一个变量；L[0] 为全部
    ///     <U4 [1] 1001>               // VID：变量 ID
    /// >
    /// .
    /// 'S1F12'                         // 设备→Host，变量名录应答
    /// <L [1]                          // 一条变量说明
    ///     <L [3]                      // 当前实现的三个字段
    ///         <A [4] '1001'>          // ID：当前回复使用 ASCII
    ///         <A [5] 'State'>         // Name：变量名称
    ///         <A [2] 'U4'>            // DataTypeName：数据类型名，不是单位
    ///     >
    /// >
    /// .
    /// ]]></code>
    /// 当前回复三项实际为 ASCII ID、Name、DataTypeName（不是 UNITS）；名字/类型依配置。L[0] 请求全部。
    /// </remarks>
    [SecsOrder("S1F11")]
    class S1F11 : SecsOrderBase
    {
       
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、Add、SetItem、GetVIDByID、L；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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
                return Fail(ex);    
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
