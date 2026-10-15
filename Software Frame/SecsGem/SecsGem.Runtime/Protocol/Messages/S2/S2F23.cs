// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S2F23Info、S2F23。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Extension.Core.Concurrency、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Extension.Core.Concurrency;
using Prism.Ioc;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SecsGem.Runtime.Order.S2
{

    /// <summary>
    /// S2F23Info 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// </summary>
    public struct S2F23Info
    {
        /// <summary>
        /// ID 
        /// </summary>
        public string TRID { get; set; }
        /// <summary>
        /// 采样间隔，单位毫秒
        /// </summary>
        public string DSPER { get; set; }
        /// <summary>
        /// 总采样数
        /// </summary>
        public string TOTSMP { get; set; }
        /// <summary>
        /// 告分组大小，每5个采样生成一个报告
        /// </summary>
        public string REPGSZ { get; set; }
        /// <summary>
        /// VID ID集合
        /// </summary>
        public List<string>  VID { get; set; }
    }

    /// <summary>
    /// Trace Process sensor H->E  
    /// </summary>
    /// <remarks>
    /// S2F23 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：SecsOrderBase。具体约束按这些契约及各成员实现解释。
    /// </remarks>
    [SecsOrder("S2F23")]
    class S2F23 : SecsOrderBase
    {
    
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、ToList、Skip、B、Run；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage )
        {
            try
            {
                var data = SecsItemToList(pSecsMessage.SecsItem);
       
                S2F23Info s2F23 = new S2F23Info()
                {
                    TRID = data[0],
                    DSPER = data[1],
                    TOTSMP = data[2],
                    REPGSZ = data[3],
                    VID = data.Skip(4).ToList(),
                };
 
                Item SecsItem = Item.B(0); //Success;  

                Task.Run(() =>
                {
                    // 触发字典中对应指令的事件，并传递s2F23数据对象
                    EventHelper.TriggerEvent("S2F23", s2F23);
                });

                return SecsItem;
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }
    }
}

