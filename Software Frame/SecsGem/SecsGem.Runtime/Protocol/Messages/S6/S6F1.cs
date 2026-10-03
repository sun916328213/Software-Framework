// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SS6F1、S6F1。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Extension.Core.Conversion、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Extension.Core.Conversion;
using Prism.Ioc;
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
        /// TRID
        /// </summary>
        public string TRID { get; set; }
        /// <summary>
        /// Number
        /// </summary>
        public string SMPLN { get; set; }
        /// <summary>
        /// 时间 yyyymmyyddhhmmsscc
        /// </summary>
        public string STIME { get; set; }
        /// <summary>
        /// VID ID集合
        /// </summary>
        public List<string> VID { get; set; }
    }

    /// <summary>
    /// S6F1 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：SecsOrderBase。具体约束按这些契约及各成员实现解释。
    /// </summary>
    [SecsOrder("S6F1")]
    class   S6F1:SecsOrderBase
    {
       
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetSendItem。
        /// 正文中的关键协作调用包括 GetVIDByID、AddData、Add、ToYYYYMMDDHHMMSSFFF、L；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetSendItem(params object[] pData )
        {
            try
            {
                if (pData == null) return null;


                SS6F1 s2F23 = (SS6F1)pData[0];
                VIDManager vIDManager = (VIDManager)pData[1];

                List<Item> vid_vlaue = new List<Item>();

                foreach (var item in s2F23.VID)
                {
                    var vid = vIDManager.GetVIDByID(item);
                    Item viditem = AddData(vid.Value, vid.DataType);
                    vid_vlaue.Add(viditem);
                }

                Item TRID = AddData(s2F23.TRID, SecsFormat.U2);
                Item SMPLN = AddData(s2F23.SMPLN, SecsFormat.U2);
                Item time = AddData(DateTime.Now.ToYYYYMMDDHHMMSSFFF(), SecsFormat.ASCII);
                Item SecsItem = Item.L(TRID, SMPLN, time, Item.L(vid_vlaue.ToArray()));

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
