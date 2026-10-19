// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S2F35。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Prism.Ioc、Secs4Net。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Prism.Ioc;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SecsGem.Runtime.Order.S2
{
    /// <summary>  
    /// 修改 CEID 关联的 ReportID（事件与报表链接），H→E。  
    /// </summary>  
      /// <remarks>
    /// 指令格式示例：DATAID=1，将已有事件 CEID=101 链接到已有报告 RPTID=100；会修改并保存配置。
    /// <code><![CDATA[
    /// 'S2F35'W                        // Host→设备，修改事件与报告链接
    /// <L [2]                          // DATAID、事件链接列表
    ///     <U4 [1] 1>                  // DATAID：本次链接定义标识
    ///     <L [1]                      // 一组事件链接
    ///         <L [2]                  // CEID、RPTID 列表
    ///             <U4 [1] 101>        // CEID：已有事件 ID
    ///             <L [1]              // 关联一个已有报告
    ///                 <U4 [1] 100>    // RPTID：报告 ID
    ///             >
    ///         >
    ///     >
    /// >
    /// .
    /// 'S2F36'                         // 设备→Host，链接应答
    /// <B [1] 0x00>                    // LRACK：0=接受
    /// .
    /// ]]></code>
    /// LRACK=0 接受；事件和报告必须存在。本指令链接 CEID→RPTID，S2F33 才是 RPTID→VID。
    /// </remarks>
    [SecsOrder("S2F35")]
    class S2F35 : SecsOrderBase
    {
     

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、ToList、Skip、Modify、Update；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage )
        {
            try
            {
                // 按组解析，不用 SecsItemToList 平铺：一条里可能有多组 <DATAID, CEID, L[RPTID]>
                // （产线 Host 实测 CEID 105 与 CEID 104 同发），平铺后第二个 CEID
                // 会被当成报表 ID 挂到第一个事件上，然后报 S9F7。
                var groups = SplitLinkGroups(pSecsMessage.SecsItem);
                if (groups.Count == 0) throw new Exception("S2F35 请求体解析不出任何一组链接");

                // 同 S2F33：先整条校验再落盘，别留下半截链接配置。
                foreach (var g in groups)
                {
                    if (!VIDManager.CEID.Datas.Any(c => c.ID == g.LinkId))
                        throw new Exception($"Collection Event ID 不存在: {g.LinkId}");

                    var missing = g.Members.Where(m => !VIDManager.ReportID.Datas.Any(r => r.ID == m)).ToArray();
                    if (missing.Length != 0)
                        throw new Exception($"事件 {g.LinkId} 的 Report 不存在: {string.Join(",", missing)}");
                }

                foreach (var g in groups)
                {
                    // S2F35 是"事件↔报表链接"：改的是 CEID 挂的报表，不是报表挂的变量。
                    // 原实现误调 ReportID.Modify(eventid, ...)，事件 ID 在报表里永远找不到，
                    // Host 的链接请求静默无事发生，还回 Success。CEID.Modify 找不到会抛，转 S9F7。
                    VIDManager.CEID.Modify(g.LinkId, g.Members);
                }

                //刷新数据
                VIDManager.ReportID.Update(VIDManager.VIDList.ToList()); //刷新ReportID数据
                VIDManager.CEID.Update(VIDManager.ReportID.GetDatas());//刷新CEID数据

                Item SecsItem = Item.B(0); //Success;  
                return SecsItem;
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7，原因进 SEMILog
            }
        }
    }
}
