// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S2F33。成员说明可结合下方具体实现阅读。
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
    /// 修改ReportID中的VID H->E  
    /// </summary>  
     /// <remarks>
    /// 指令格式示例：DATAID=1，将已有报告 RPTID=100 关联到已有 VID=1001、1002；会修改并保存配置。
    /// <code><![CDATA[
    /// 'S2F33'W                        // Host→设备，修改报告定义
    /// <L [2]                          // DATAID、报告定义列表
    ///     <U4 [1] 1>                  // DATAID：本次定义标识
    ///     <L [1]                      // 一组报告定义
    ///         <L [2]                  // RPTID、VID 列表
    ///             <U4 [1] 100>        // RPTID：已有报告 ID
    ///             <L [2]              // 报告关联两个已有变量
    ///                 <U4 [1] 1001>   // VID：第一个变量
    ///                 <U4 [1] 1002>   // VID：第二个变量
    ///             >
    ///         >
    ///     >
    /// >
    /// .
    /// 'S2F34'                         // 设备→Host，报告定义应答
    /// <B [1] 0x00>                    // DRACK：0=接受
    /// .
    /// ]]></code>
    /// DRACK=0 接受；报告/VID 必须已经存在。当前是修改既有报告的实现，不把示例当成任意新建报告能力。
    /// </remarks>
    [SecsOrder("S2F33")]
    class S2F33 : SecsOrderBase
    {
      
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、ToList、Skip、Modify、Update；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                // 按组解析，不用 SecsItemToList 平铺：一条里可能有多组 <DATAID, RPTID, L[VID]>
                // （产线 Host 实测），平铺后第二组的 RPTID 会被当成 VID 塞进第一组。
                var groups = SplitLinkGroups(pSecsMessage.SecsItem);
                if (groups.Count == 0) throw new Exception("S2F33 请求体解析不出任何一组定义");

                // 先整条校验，任一组引用不成立就抛、一条都不改。
                // 原来的顺序是"先 Modify 落盘、再由 Update 抛"，会把含无效 VID 的半截配置
                // 写进 CSV，之后每次组包（S6F11/S1F23）都在同一处失败 —— 现场看着像配置整体坏了。
                foreach (var g in groups)
                {
                    if (!VIDManager.ReportID.Datas.Any(r => r.ID == g.LinkId))
                        throw new Exception($"Report ID 不存在: {g.LinkId}");

                    var missing = g.Members.Where(m => VIDManager.GetVIDByID(m) == null).ToArray();
                    if (missing.Length != 0)
                        throw new Exception($"Report {g.LinkId} 的 VID 不存在: {string.Join(",", missing)}");
                }

                foreach (var g in groups)
                {
                    //修改ReportID挂载数据（组内重复 ID 在 Modify 里去重保序）
                    VIDManager.ReportID.Modify(g.LinkId, g.Members);
                }

                //刷新ReportID数据
                VIDManager.ReportID.Update(VIDManager.VIDList.ToList());

                //刷新CEID数据
                VIDManager.CEID.Update(VIDManager.ReportID.GetDatas());

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
