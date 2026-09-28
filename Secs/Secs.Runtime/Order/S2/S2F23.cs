using Extension.Core.Concurrency;
using Prism.Ioc;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Secs.Order.S2
{

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
    [SecsOrder("S2F23")]
    class S2F23 : SecsOrderBase
    {
    
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
                string message = ex.Message + ex.StackTrace;
                return null;  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }
    }
}

