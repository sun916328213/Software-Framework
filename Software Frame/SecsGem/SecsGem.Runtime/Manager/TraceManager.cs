// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：TraceManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、Extension.Core.Concurrency、Prism.Ioc、SecsGem.Runtime.Order.S2、System。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime;
using Extension.Core.Concurrency;
using Prism.Ioc;
using SecsGem.Runtime.Order.S2;
using System;

namespace SecsGem.Runtime.Manager
{
    /// <summary>
    /// Trace的管理类 S2F23+S6F1
    /// </summary>
    public class TraceManager : ManagerBase
    {

        /// <summary>
        /// 初始化 TraceManager 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public TraceManager(IContainerProvider container) : base(container)
        {
        }

        /// <summary>
        /// 注册委托函数
        /// </summary>
        /// <param name="action">刷新数据</param>
        public void Register(Action<CEventArgs> action)
        {
            //注册委托
            EventHelper.RegisterEvent("S2F23", action);
        }

        /// <summary>
        /// 发送Trace 
        /// </summary>
        /// <param name="pS2F23Data"></param>
        public void Send(S2F23Info pS2F23Data, Action action)
        {
            var S2F23Data = pS2F23Data;
            action += GetData;
            TaskEx.TimesRun(action, Convert.ToInt32(S2F23Data.DSPER), Convert.ToInt32(S2F23Data.TOTSMP));
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetData。
        /// </summary>
        private async void GetData()
        {
            //await SecsGemRuntime.SendItem("S6F1", true, new object[] { S2F23Data, VIDManager });
        }
    }
}
