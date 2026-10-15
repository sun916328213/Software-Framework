// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S2F37。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Prism.Ioc、SecsGem.Runtime.Variable。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Prism.Ioc;
using SecsGem.Runtime.Variable;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SecsGem.Runtime.Order.S2
{ 
    /// <summary>  
  /// 开启或者关闭Event H->E  
  /// </summary>  
      [SecsOrder("S2F37")]
    class S2F37 : SecsOrderBase
    {
     
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、ToBoolean、ToList、Skip、FirstOrDefault；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                var data = SecsItemToList(pSecsMessage.SecsItem);
          
                bool isEnable = Convert.ToBoolean(data[0]);
                List<string> eventid = data.Skip(1).ToList();
 
                foreach (var item in eventid)
                {
                    CEIDInfo ceid = VIDManager.CEID.GetDatas().FirstOrDefault(x => x.ID == item);
                    if (ceid != null)
                    {
                        ceid.Enable = isEnable;
                    }
                }

                Item SecsItem = Item.B(0); //Success;  
                return SecsItem;
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }
    }
}
