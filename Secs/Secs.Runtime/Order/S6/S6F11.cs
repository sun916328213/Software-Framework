using Extension.Core.Conversion;
using Prism.Ioc;
using Secs.Manager;
using Secs.Variable;
using Secs4Net;
using System;
using System.Collections.Generic;

namespace Secs.Order.S6
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
    [SecsOrder("S6F11")]
    class S6F11:SecsOrderBase
    {
        int DataID = 0;

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
                string message = ex.Message + ex.StackTrace;
                return null;  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }
    }
}
