// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S1F23。成员说明可结合下方具体实现阅读。
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
    /// 采集事件名录请求 Collection Event Namelist Request H->E
    /// </summary>
    /// <remarks>
    /// 指令格式示例：假设已配置事件 CEID=101（名称 Ready），关联 VID=1001、1002。
    /// <code><![CDATA[
    /// 'S1F23'W                        // Host→设备，请求事件名录
    /// <L [1]                          // 请求一个事件；L[0] 为全部
    ///     <U4 [1] 101>                // CEID：事件 ID
    /// >
    /// .
    /// 'S1F24'                         // 设备→Host，事件名录应答
    /// <L [1]                          // 一条事件说明
    ///     <L [3]                      // CEID、CENAME、关联 VID
    ///         <U4 [1] 101>            // CEID：事件 ID
    ///         <A [5] 'Ready'>         // CENAME：事件名称
    ///         <L [2]                  // 事件关联的变量，去重保序
    ///             <U4 [1] 1001>       // VID：第一个变量 ID
    ///             <U4 [1] 1002>       // VID：第二个变量 ID
    ///         >
    ///     >
    /// >
    /// .
    /// ]]></code>
    /// 三项为 CEID、CENAME、去重后的关联 VID 列表；L[0] 查询全部，未知 CEID 跳过。
    /// </remarks>
    [SecsOrder("S1F23")]
    class S1F23 : SecsOrderBase
    {
        /// <summary>
        /// 收到 S1F23（Host 要事件名录）：回 S1F24。
        /// <para>
        /// 请求体是 CEID 列表（U4），空列表 = 全要；点名要 = 按 ID 过滤，不认识的跳过。
        /// 回包结构（E5 S1F24）：L[n] &lt;L[3] &lt;U4 CEID&gt; &lt;A CENAME&gt; &lt;L[a] &lt;U4 VID&gt;&gt;&gt;。
        /// CENAME 暂用 CEID 的 Name 顶（表里没有单独的 CENAME 列）。
        /// 每个事件的 VID 取自它挂的报表（去重保序），先走一遍 <c>UpdateCEID</c> 重建关联，
        /// 和 S6F11 组包是同一条路。
        /// </para>
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                VIDManager.UpdateCEID();

                var all = VIDManager.CEID.GetDatas();
                var wanted = SecsItemToList(pSecsMessage.SecsItem);
                var selected = (wanted == null || wanted.Count == 0)
                    ? all
                    : all.Where(c => wanted.Contains(c.ID)).ToList();

                var events = new List<Item>();
                foreach (var ceid in selected)
                {
                    var vids = (ceid.ReportInfo ?? new List<ReportInfo>())
                        .SelectMany(r => r.VIDInfo ?? new List<VIDInfo>())
                        .Select(v => v.ID)
                        .Distinct()
                        .Select(id => AddData(id, SecsFormat.U4))
                        .ToArray();
                    events.Add(Item.L(
                        AddData(ceid.ID, SecsFormat.U4),
                        Item.A(ceid.Name ?? string.Empty),
                        Item.L(vids)));
                }
                return Item.L(events.ToArray());
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null 触发S9F7 
            }
        }

        /// <summary>
        /// 获取发送数据。S1F23 是事件名录请求，按 E5 只能 Host→Equipment，
        /// 设备端不发起，返回 null 让引擎明确报"该指令不可发"而不是组个空包出去。
        /// </summary>
        public override Item GetSendItem(params object[] pData)
        {
            return null;
        }
    }
}
