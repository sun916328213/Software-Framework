// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S6F11Info、S6F11。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Extension.Core.Conversion、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Extension.Core.Conversion;
using Prism.Ioc;
using SecsGem.Runtime.Manager;
using SecsGem.Runtime.Variable;
using Secs4Net;
using System;
using System.Collections.Generic;

namespace SecsGem.Runtime.Order.S6
{
    /// <summary>
    /// S6F11发送输入参数结构体
    /// </summary>
    public struct S6F11Info
    {
        /// <summary>
        /// CEID类
        /// </summary>
        public CEIDInfo CEID { get; set; }

    }

    /// <summary>
    /// 发送事件 E->H
    /// </summary>
    /// <remarks>
    /// 指令格式示例：假设已配置 CEID=101、RPTID=100，报告包含 U4 和 ASCII 两个有效值。
    /// <code><![CDATA[
    /// 'S6F11'W                        // 设备→Host，事件上报，请求确认
    /// <L [3]                          // DATAID、CEID、报告列表
    ///     <A [1] '1'>                 // DATAID：当前为 ASCII 计数，1～100 循环
    ///     <U2 [1] 101>                // CEID：当前事件 ID 使用 U2
    ///     <L [1]                      // 一份关联报告
    ///         <L [2]                  // RPTID、报告值列表
    ///             <U2 [1] 100>        // RPTID：当前报告 ID 使用 U2
    ///             <L [2]              // V：按报告配置顺序，只含值不含 VID
    ///                 <U4 [1] 1>      // 第一个变量值，类型依配置
    ///                 <A [4] 'IDLE'>  // 第二个变量值，类型依配置
    ///             >
    ///         >
    ///     >
    /// >
    /// .
    /// 'S6F12'                         // Host→设备，事件确认示例
    /// <B [1] 0x00>                    // ACKC6：Host 接受；实际码由 Host 返回
    /// .
    /// ]]></code>
    /// DATAID/CEID/RPTID 的上述类型是当前实现，不假装全部为 U4；当前 null 变量值会被跳过，实际报告数量取自配置。
    /// </remarks>
    [SecsOrder("S6F11")]
    class S6F11:SecsOrderBase
    {
        int DataID = 0;

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetSendItem。
        /// 正文中的关键协作调用包括 UpdateVID、ToYYYYMMDDHHMMSSFFF、UpdateCEID、Clear、AddData；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetSendItem( params object[] pData  )
        {
            try
            {
                VIDManager _VIDManager = (VIDManager)pData[1];
                _VIDManager.UpdateVID("Clock", DateTime.Now.ToYYYYMMDDHHMMSSFFF());
                _VIDManager.UpdateCEID();

                if (pData == null) return null;

                if (DataID == 100) DataID = 0;
                DataID++;
              
                CEIDInfo _CEID = ((S6F11Info)pData[0]).CEID;
 
                //vid集合
                List<Item> vidItems = new List<Item>();
                ///  report集合
                List<Item> reportsItems = new List<Item>();

                //report信息
                List<ReportInfo> reports = _CEID.ReportInfo;

                foreach (ReportInfo report in reports)
                {
                    vidItems.Clear();
                    //遍历Report中的VID
                    foreach (VIDInfo vid in report.VIDInfo)
                    {
                        if (vid.Value != null)
                        {
                            //添加VID
                            Item viditem = AddData(vid.Value, vid.DataType);
                            vidItems.Add(viditem);
                        }
                    }
                    Item reportitem = AddData(report.ID, SecsFormat.U2);
                    reportsItems.Add(Item.L(reportitem, Item.L(vidItems.ToArray())));
                }
                Item dataid = AddData(DataID, SecsFormat.ASCII);
                Item ceid = AddData(_CEID.ID, SecsFormat.U2);
                Item SecsItem = Item.L(
                    dataid,
                  ceid,
                    Item.L(reportsItems.ToArray())
                    );
                return SecsItem;
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }
    }
}
