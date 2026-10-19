// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S1F3。成员说明可结合下方具体实现阅读。
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
using System.Linq;

namespace SecsGem.Runtime.Order.S1
{
    /// <summary>
    /// 获取所选设备状态请求 H->E。本设备按 SVID/DVID/ECID 全表解析 ID，
    /// 空请求回全部三类变量（见方法内注释）。
    /// </summary>
    /// <remarks>
    /// 指令格式示例：请求已配置的 VID=1001、1002，假设其类型分别为 U4、ASCII。
    /// <code><![CDATA[
    /// 'S1F3'W                         // Host→设备，请求应答
    /// <L [2]                          // 查询两个已配置变量
    ///     <U4 [1] 1001>               // VID：第一个变量 ID
    ///     <U4 [1] 1002>               // VID：第二个变量 ID
    /// >
    /// .
    /// 'S1F4'                          // 设备→Host，变量值应答
    /// <L [2]                          // 仅含值，类型由配置决定
    ///     <U4 [1] 1>                  // VID=1001 的示例值
    ///     <A [4] 'IDLE'>              // VID=1002 的示例值
    /// >
    /// .
    /// ]]></code>
    /// 回复只含值，不带 ID；值类型取自配置，未知 ID 当前跳过。L[0] 请求全部变量；本实现包括 SVID/DVID/ECID。
    /// </remarks>
    [SecsOrder("S1F3")]
    class S1F3 : SecsOrderBase
    {
       
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、ToList、Add、ToDictionary、ContainsKey；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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
                    Dictionary<string, VIDInfo> vids = _VID.ToDictionary(s => s.ID, s => s);
                    foreach (var item in data)
                    {
                        if (vids.ContainsKey(item))
                            Add(ref Items, vids[item]);
                    }
                }
                // 注意：本设备 S1F3 按全表解析 ID，Host 可以点名 SVID/DVID/ECID 任意一类，
                // 空请求回全部三类。这是设备特性（ID 跨表全局唯一，所以裸 ID 无歧义），不是 bug，
                // 不要"收敛"成只回 SVID。

                Item SecsItem = Item.L(Items.ToArray());
                return SecsItem;
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }

        /// <summary>
        /// 执行添加操作；对应入口为 Add。
        /// 正文中的关键协作调用包括 AddData、Add；具体条件和先后次序见方法体。
        /// </summary>
        public void  Add(ref List<Item> Items,VIDInfo data)
        {
             var d1= AddData(data.Value, data.DataType);
            
                Items.Add(d1);
         
        }
    }
}
