// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S2F13。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core.Order、SecsGem.Runtime.Order、SecsGem.Runtime.Variable、Secs4Net、System、System.Collections.Generic、System.Linq。导入不代表所有类型都会在运行时加载。
using SecsGem.Core.Order;
using SecsGem.Runtime.Order;
using SecsGem.Runtime.Variable;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SecsGem.Runtime.Order.S2
{
    /// <summary>
    /// 设备常量请求 Equipment Constant Request H->E（E5 S2F13）。
    /// <para>
    /// 请求体 <c>L[n] &lt;ECID&gt;</c>，空列表 = 全要；回 S2F14 <c>L[n] &lt;ECV&gt;</c>。
    /// ECV 与请求里的 ECID 一一对应、同序；取值用 ECID 自己的数据类型（动态类型）。
    /// 请求里出现不存在的 ECID 时按标准回 <c>L:0</c> 占位（空列表表示"无此常量"），
    /// 而不是丢包或错位回值——错位会让 Host 把 B 的值当成 A 的。
    /// </para>
    /// </summary>
    [SecsOrder("S2F13")]
    class S2F13 : SecsOrderBase
    {
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、FindECID、AddData、L；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                var requested = SecsItemToList(pSecsMessage.SecsItem) ?? new List<string>();

                // 空请求 = 全部 ECID；点名要就按请求顺序回，顺序不能改（S2F14 与 S2F13 同序）。
                var ids = requested.Count == 0
                    ? VIDManager.ECID.GetDatas().Select(x => x.ID).ToList()
                    : requested;

                var values = new List<Item>(ids.Count);
                foreach (var id in ids)
                {
                    var ecid = FindECID(id);
                    values.Add(ecid == null ? Item.L() : AddData(ecid.Value, ecid.DataType));
                }

                return Item.L(values.ToArray());
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }
    }
}
