using Extension.Core.Concurrency;
using Prism.Ioc;
using Secs.Order.S2;
using System;

namespace Secs.Manager
{
    /// <summary>
    /// Trace的管理类 S2F23+S6F1
    /// </summary>
    public class TraceManager : ManagerBase
    {

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

        private async void GetData()
        {
            //await SecsControl.SendItem("S6F1", true, new object[] { S2F23Data, VIDManager });
        }
    }
}
