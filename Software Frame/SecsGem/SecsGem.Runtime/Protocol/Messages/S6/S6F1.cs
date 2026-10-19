// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SS6F1、S6F1。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core.Order、SecsGem.Runtime.Order、SecsGem.Runtime.Manager、Secs4Net、System、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using SecsGem.Core.Order;
using SecsGem.Runtime.Order;
using SecsGem.Runtime.Manager;
using Secs4Net;
using System;
using System.Collections.Generic;

namespace SecsGem.Runtime.Order.S6
{
    /// <summary>
    /// SS6F1 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// </summary>
    public struct SS6F1 
    {
        /// <summary>
        /// Trace 标识，必须与 S2F23 下发的一致
        /// </summary>
        public string TRID { get; set; }
        /// <summary>
        /// 采样序号，从 1 开始
        /// </summary>
        public string SMPLN { get; set; }
        /// <summary>
        /// 采样时间 yyyyMMddHHmmssff
        /// </summary>
        public string STIME { get; set; }
        /// <summary>
        /// SVID ID集合。SV 为空时按这些 ID 现场取值（兼容旧调用）。
        /// </summary>
        public List<string> VID { get; set; }
        /// <summary>
        /// 采样快照值，优先于 <see cref="VID"/>。
        /// <para>
        /// TraceManager 在采样那一刻就取好值放进这里：如果等到组包时再按 ID 取值，
        /// 组包与采样之间变量被改写，报出去的就不是那一刻的值了。
        /// </para>
        /// </summary>
        public List<Item> SV { get; set; }
        /// <summary>
        /// TRID 的格式，取 S2F23 下发时的格式。
        /// </summary>
        public SecsFormat TRIDType { get; set; }
    }

    /// <summary>
    /// Trace Data Send E->H（E5 S6F1）。
    /// <para>
    /// 报文体 <c>L[4] &lt;TRID&gt; &lt;SMPLN&gt; &lt;STIME&gt; &lt;L[n] SV&gt;</c>。
    /// TRID 格式与 S2F23 一致、SMPLN 用 U4、STIME 用 16 位 <c>yyyyMMddHHmmssff</c>
    /// （与 S2F18 的 TIME 同格式；GEM 里 STIME 的具体格式本应由 TimeFormat ECV 决定，本设备统一取值）。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 指令格式示例：对应 S2F23 TRID=1 的第一个采样；TraceManager 当前用 W=0 发送。
    /// <code><![CDATA[
    /// 'S6F1'                          // 设备→Host，W=0，当前不等待 S6F2
    /// <L [4]                          // TRID、SMPLN、STIME、采样值列表
    ///     <U4 [1] 1>                  // TRID：类型保持与 S2F23 下发一致
    ///     <U4 [1] 1>                  // SMPLN：采样序号，从 1 开始
    ///     <A [16] '2026010212345600'>  // STIME：yyyyMMddHHmmssff，本地采样时间
    ///     <L [2]                      // 两个采样值，顺序对应请求的 SVID
    ///         <U4 [1] 1>              // SV：第一个变量的快照值
    ///         <A [4] 'IDLE'>          // SV：第二个变量的快照值
    ///     >
    /// >
    /// .
    /// ]]></code>
    /// SVID 数量/类型依实际 Trace 配置；本例需下发两个 SVID。若其他调用方显式选择 W=1，确认消息为 S6F2，ACKC6 为 B[1]。
    /// </remarks>
    [SecsOrder("S6F1")]
    class   S6F1:SecsOrderBase
    {
       
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetSendItem。
        /// 正文中的关键协作调用包括 GetVIDByID、AddData、Add、L；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetSendItem(params object[] pData )
        {
            try
            {
                if (pData == null || pData.Length == 0) return null;


                SS6F1 data = (SS6F1)pData[0];

                List<Item> sv;
                if (data.SV != null)
                {
                    // TraceManager 走这条路：值在采样时已取好。
                    sv = data.SV;
                }
                else
                {
                    // 兼容旧调用：只给 ID，按当前值现场取值。
                    VIDManager vIDManager = (VIDManager)pData[1];
                    sv = new List<Item>();
                    foreach (var item in data.VID ?? new List<string>())
                    {
                        var vid = vIDManager.GetVIDByID(item);
                        sv.Add(AddData(vid?.Value, vid?.DataType ?? SecsFormat.ASCII));
                    }
                }

                // SecsFormat 的默认值 0 是 List：没显式给 TRIDType 时落到 U4，
                // 否则 AddData 会走 List 分支返回 null，整条 S6F1 组包失败。
                var tridType = data.TRIDType == SecsFormat.List ? SecsFormat.U4 : data.TRIDType;
                Item TRID = AddData(data.TRID, tridType);
                Item SMPLN = AddData(data.SMPLN, SecsFormat.U4);
                Item time = AddData(
                    string.IsNullOrEmpty(data.STIME) ? DateTime.Now.ToString("yyyyMMddHHmmssff") : data.STIME,
                    SecsFormat.ASCII);

                return Item.L(TRID, SMPLN, time, Item.L(sv.ToArray()));
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }
    }
}
