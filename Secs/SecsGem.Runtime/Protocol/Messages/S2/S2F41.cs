using Prism.Ioc;
using Secs.Manager;
using Secs.Variable.Data;
using Secs4Net;
using System;
using System.Collections.Generic;
 

namespace Secs.Order.S2
{
 
    /// <summary>  
    /// 主机命令发送/主机命令确认 H->E  
    /// </summary>  
    [SecsOrder("S2F41")]
    class S2F41 : SecsOrderBase
    {
        public override Item GetReplyItem(SecsMessage pSecsMessage   )
        {
            try
            {
                var data = SecsItemToList(pSecsMessage.SecsItem);

                ActiveReceiveInfo sS2F41 = new ActiveReceiveInfo("S2F41", data[0], data);

                VIDManager.ActiveReceive.AddData(sS2F41);

                List<Item> list = new List<Item>();
                for (int index = 1; index < data.Count; index = index + 2)
                {
                    Item[] item = new Item[2];
                    item[0] = Item.A(data[index + 0]);
                    item[1] = Item.B(0);
                    list.Add(Item.L(item));
                }
 
                Item SecsItem = Item.L
                (
                    Item.B(0),
                       Item.L(list.ToArray())
                );
                return SecsItem;
            }
            catch (Exception ex)
            {
                string message = ex.Message + ex.StackTrace;
                return null;  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }

        /// <summary>
        /// 异步处理：解析 RCMD/参数 → 回调业务方 ISecsControlEvents.OnRemoteCommandAsync → 按其 Return(HCACK) 组 S2F42。
        /// 同时保留旧 ActiveReceive 队列写入，兼容存量轮询代码。
        /// </summary>
        public override async System.Threading.Tasks.Task<Item> GetReplyItemAsync(SecsMessage pSecsMessage)
        {
            try
            {
                var data = SecsItemToList(pSecsMessage.SecsItem);

                // 兼容旧通道
                VIDManager.ActiveReceive.AddData(new ActiveReceiveInfo("S2F41", data[0], data));

                // 新回调通道
                RemoteCommandArgs args = new RemoteCommandArgs { RCMD = data[0], Return = 0 };
                for (int index = 1; index + 1 < data.Count; index = index + 2)
                {
                    args.Params[data[index]] = data[index + 1];
                }

                await SecsControl.RaiseRemoteCommandAsync(args).ConfigureAwait(false);

                List<Item> list = new List<Item>(args.Params.Count);
                foreach (var kvp in args.Params)
                {
                    list.Add(Item.L(Item.A(kvp.Key), Item.B(0)));
                }

                return Item.L(Item.B(args.Return), Item.L(list.ToArray()));
            }
            catch (Exception)
            {
                return null;  // 解析或回调失败 返回null 触发S9F7
            }
        }
    }
}
