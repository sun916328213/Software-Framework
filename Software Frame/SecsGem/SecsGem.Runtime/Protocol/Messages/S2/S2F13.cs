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
    /// <remarks>
    /// 指令格式示例：假设 ECID=3001 是 U4 常量，3002 是 ASCII 常量。
    /// <code><![CDATA[
    /// 'S2F13'W                        // Host→设备，读取常量
    /// <L [2]                          // 两个 ECID；L[0] 为全部
    ///     <U4 [1] 3001>               // ECID：第一个常量
    ///     <U4 [1] 3002>               // ECID：第二个常量
    /// >
    /// .
    /// 'S2F14'                         // 设备→Host，常量值应答
    /// <L [2]                          // 按请求顺序返回 ECV
    ///     <U4 [1] 100>                // ECV：ECID=3001 的示例值
    ///     <A [4] 'AUTO'>              // ECV：ECID=3002 的示例值
    /// >
    /// .
    /// ]]></code>
    /// 请求为 ECID 列表，回复为同序 ECV，不带 ECID；类型/值依配置。未知 ID 对应 L[0] 占位；空请求取全部。
    /// </remarks>
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
