// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S5F3。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Prism.Ioc、SecsGem.Runtime.Variable。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Prism.Ioc;
using SecsGem.Runtime.Variable;
using Secs4Net;
using System;
using System.Linq;

namespace SecsGem.Runtime.Order.S5
{
    /// <summary>
    /// 启用报警信息 H->E
    /// </summary>
    [SecsOrder("S5F3")]
    class S5F3 : SecsOrderBase
    {
     

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、Select、GetDatas、Contains、Split；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage   )
        {
            Item SecsItem;
            try
            {
                //解析数据
                var data = SecsItemToList(pSecsMessage.SecsItem);
 
                //格式
                bool enable = data[0] == "1"?true:false ;
                string ids = data[1];

                Alarm alarm = VIDManager. Alarm;

                if (ids.Length == 0)
                {
                    alarm.GetDatas().Select(s => s.Enable = enable);
                }
                else if (ids.Contains(","))  ////多个报警信息
                {
                    var idarray = ids.Split(',');
                    //遍历_alarmcopy 将匹配的报警信息启用
                    foreach (var id in idarray)
                    {
                        alarm.SetEnable(id, enable);
                    }
                }
                else  //单个报警信息
                {
                    alarm.SetEnable(ids, enable);
                }
                alarm.Save();
                SecsItem = Item.B(0);//succsee
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
