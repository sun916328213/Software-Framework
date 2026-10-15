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
