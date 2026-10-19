// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S2F23Info、S2F23。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core.Order、SecsGem.Runtime.Order、SecsGem.Runtime.Manager、Secs4Net、System、System.Collections.Generic、System.Linq。导入不代表所有类型都会在运行时加载。
using SecsGem.Core.Order;
using SecsGem.Runtime.Order;
using SecsGem.Runtime.Manager;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SecsGem.Runtime.Order.S2
{

    /// <summary>
    /// S2F23Info 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// </summary>
    public struct S2F23Info
    {
        /// <summary>
        /// Trace 标识，同一条 Trace 的 S6F1 都用它
        /// </summary>
        public string TRID { get; set; }
        /// <summary>
        /// 数据采样周期，标准为 A[6] "hhmmss"（可选 A[8] "hhmmsscc"）。
        /// 历史写法是纯数字毫秒，<c>TraceManager.TryParseDsper</c> 两种都认。
        /// </summary>
        public string DSPER { get; set; }
        /// <summary>
        /// 总采样数；0 表示终止该 TRID 的 Trace
        /// </summary>
        public string TOTSMP { get; set; }
        /// <summary>
        /// 报告分组大小，每 REPGSZ 个采样发一条 S6F1；0 表示每个采样各发一条
        /// </summary>
        public string REPGSZ { get; set; }
        /// <summary>
        /// SVID ID集合
        /// </summary>
        public List<string> VID { get; set; }
        /// <summary>
        /// TRID 在请求里的格式。S6F1 的 TRID 要与 S2F23 下发时一致，不能固定写死。
        /// </summary>
        public SecsFormat TRIDType { get; set; }
    }

    /// <summary>
    /// Trace Initialize Send H->E（E5 S2F23），回 S2F24 TIAACK；采样数据由 S6F1 周期上报。
    /// <para>
    /// 请求体 <c>L[5] &lt;TRID&gt; &lt;DSPER&gt; &lt;TOTSMP&gt; &lt;REPGSZ&gt; &lt;L[n] SVID&gt;</c>。
    /// 校验与 trace 的启停都交给 <see cref="TraceManager"/>，本处理器只负责解析请求体、
    /// 把 TIAACK 回出去。
    /// </para>
    /// <para>
    /// 原实现把请求转成 <c>S2F23Info</c> 后扔进 <c>EventHelper.TriggerEvent("S2F23", …)</c> 就回 B(0)，
    /// 既没人订阅、又和引擎自己触发的 "S2F23"（事件数据是回包 Item）撞在同一个事件名上；
    /// 现在改成直接调 TraceManager，去掉这条没有消费者的广播。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 指令格式示例：TRID=1，每秒采样一次，共 10 次，每条报告一个采样，采集已配置 VID=1001、1002。
    /// <code><![CDATA[
    /// 'S2F23'W                        // Host→设备，启动/停止 Trace
    /// <L [5]                          // 五个 Trace 参数
    ///     <U4 [1] 1>                  // TRID：Trace 标识
    ///     <A [6] '000001'>            // DSPER：hhmmss，此例每秒一次
    ///     <U4 [1] 10>                 // TOTSMP：采样总数；0=停止该 TRID
    ///     <U4 [1] 1>                  // REPGSZ：每个报告的采样数
    ///     <L [2]                      // 待采集的两个 SVID，对应 S6F1 示例
    ///         <U4 [1] 1001>           // SVID：第一个已有变量
    ///         <U4 [1] 1002>           // SVID：第二个已有变量
    ///     >
    /// >
    /// .
    /// 'S2F24'                         // 设备→Host，Trace 初始化应答
    /// <B [1] 0x00>                    // TIAACK：0=接受，其余见 TraceManager
    /// .
    /// ]]></code>
    /// 五项为 TRID、DSPER、TOTSMP、REPGSZ、SVID 列表；TIAACK=0 接受。TOTSMP=0 终止对应 Trace，后续数据见 S6F1。
    /// </remarks>
    [SecsOrder("S2F23")]
    class S2F23 : SecsOrderBase
    {
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、Skip、Start、B；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                var data = SecsItemToList(pSecsMessage.SecsItem);
                if (data == null || data.Count < 4)
                    throw new Exception("S2F23 请求体不足：至少要有 TRID/DSPER/TOTSMP/REPGSZ 四项");

                S2F23Info s2F23 = new S2F23Info()
                {
                    TRID = data[0],
                    DSPER = data[1],
                    TOTSMP = data[2],
                    REPGSZ = data[3],
                    VID = data.Skip(4).ToList(),
                    TRIDType = ResolveTridFormat(pSecsMessage.SecsItem),
                };

                // S2F24 的 TIAACK：0=接受，其余见 TraceManager.Start 的返回码说明。
                byte tiaack = TraceManager?.Start(s2F23) ?? 3; // 3 = 周期非法（拿不到 TraceManager 时不可能接受）
                return Item.B(tiaack);
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }

        /// <summary>
        /// 取 TRID 的格式（请求体 L[5] 的第一项）。SecsFormat 的默认值 0 是 List，
        /// 这里必须挡掉——否则 S6F1 组包会走 List 分支，把 TRID 字符串弄丢。
        /// </summary>
        private static SecsFormat ResolveTridFormat(Item body)
        {
            if (body?.Format == SecsFormat.List && body.Items.Length > 0 && body.Items[0].Format != SecsFormat.List)
                return body.Items[0].Format;

            return SecsFormat.U4;
        }
    }
}
