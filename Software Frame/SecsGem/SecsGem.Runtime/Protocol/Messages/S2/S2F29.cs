// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S2F29。成员说明可结合下方具体实现阅读。
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
    /// 设备常量名录请求 Equipment Constant Namelist Request H->E（E5 S2F29）。
    /// <para>
    /// 请求体 <c>L[n] &lt;ECID&gt;</c>，空列表 = 全要；回 S2F30
    /// <c>L[n] &lt;L[6] &lt;ECID&gt; &lt;ECNAME&gt; &lt;ECMIN&gt; &lt;ECMAX&gt; &lt;ECDEF&gt; &lt;UNITS&gt;&gt;&gt;</c>。
    /// </para>
    /// <para>
    /// 本仓库 ECID 表只有 ID/Name/Value/DataType，没有 MIN/MAX/DEF/UNITS 四列：
    /// 按标准"未知就给空项"处理——ECV 三项回同类型的空项，UNITS 回空 ASCII。
    /// ECID 用 U4 回，与 S1F23 回 CEID 的惯例一致。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 指令格式示例：假设 ECID=3001、名称 Limit、类型 U4；当前配置未提供范围、默认值和单位。
    /// <code><![CDATA[
    /// 'S2F29'W                        // Host→设备，请求常量名录
    /// <L [1]                          // 请求一个常量；L[0] 为全部
    ///     <U4 [1] 3001>               // ECID：常量 ID
    /// >
    /// .
    /// 'S2F30'                         // 设备→Host，常量名录应答
    /// <L [1]                          // 一条常量说明
    ///     <L [6]                      // 六个固定位置字段
    ///         <U4 [1] 3001>           // ECID：常量 ID
    ///         <A [5] 'Limit'>         // ECNAME：名称
    ///         <U4 [0]>                // ECMIN：未知，空项，不是 0
    ///         <U4 [0]>                // ECMAX：未知，空项，不是 0
    ///         <U4 [0]>                // ECDEF：未知，空项，不是 0
    ///         <A [0] ''>              // UNITS：未知，空字符串
    ///     >
    /// >
    /// .
    /// ]]></code>
    /// 六项为 ECID、ECNAME、ECMIN、ECMAX、ECDEF、UNITS；三个空数值项类型随配置，不是数值 0。空请求取全部。
    /// </remarks>
    [SecsOrder("S2F29")]
    class S2F29 : SecsOrderBase
    {
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、GetDatas、Where、AddData、L；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                var requested = SecsItemToList(pSecsMessage.SecsItem) ?? new List<string>();
                var all = VIDManager.ECID.GetDatas();

                // 空请求 = 全部；点名要就按 ID 过滤，不认识的跳过（与 S1F23 同规则）。
                var selected = requested.Count == 0
                    ? all
                    : all.Where(x => requested.Contains(x.ID)).ToList();

                var list = new List<Item>(selected.Count);
                foreach (var ecid in selected)
                {
                    list.Add(Item.L(
                        AddData(ecid.ID, SecsFormat.U4),
                        Item.A(ecid.Name ?? string.Empty),
                        AddData(null, ecid.DataType),   // ECMIN 未知
                        AddData(null, ecid.DataType),   // ECMAX 未知
                        AddData(null, ecid.DataType),   // ECDEF 未知
                        Item.A(string.Empty)));         // UNITS 未知
                }

                return Item.L(list.ToArray());
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }
    }
}
