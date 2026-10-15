// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S2F41。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Prism.Ioc、SecsGem.Runtime.Manager。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Prism.Ioc;
using SecsGem.Runtime.Manager;
using SecsGem.Runtime.Variable.Data;
using Secs4Net;
using System;
using System.Collections.Generic;
 

namespace SecsGem.Runtime.Order.S2
{
 
    /// <summary>  
    /// 主机命令发送/主机命令确认 H->E  
    /// </summary>  
    [SecsOrder("S2F41")]
    class S2F41 : SecsOrderBase
    {
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、AddData、A、B、Add；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
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

                await SecsGemRuntime.RaiseRemoteCommandAsync(args).ConfigureAwait(false);

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
