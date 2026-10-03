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
    /// 修改ReportID中的VID H->E  
    /// </summary>  
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
                var data = SecsItemToList(pSecsMessage.SecsItem);
           
                //解析数据
                string dataid = data[0];
                string eventid = data[1];
                List<string> reportid = data.Skip(2).ToList();
 
                //修改ReportID挂载数据
                VIDManager. ReportID.Modify(eventid, reportid);

                //刷新数据
                VIDManager. ReportID.Update(VIDManager.VIDList.ToList()); //刷新ReportID数据
                VIDManager. CEID.Update(VIDManager. ReportID.GetDatas());//刷新CEID数据

                Item SecsItem = Item.B(0); //Success;  
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
