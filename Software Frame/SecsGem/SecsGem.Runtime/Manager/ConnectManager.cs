// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：ConnectManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、Extension.Core.Concurrency、Extension.Core.Models、Extension.Imaging.Models、Extension.Automation.Device。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime;
using Extension.Core.Concurrency;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using Prism.Ioc;
using SecsGem.Runtime.Order.S6;
using SecsGem.Runtime.Variable;
using System.Threading;
using System.Threading.Tasks;

namespace SecsGem.Runtime.Manager
{
    /// <summary>
    /// ConnectManager 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：ManagerBase。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class ConnectManager:ManagerBase
    {
        /// <summary>
        /// 初始化 ConnectManager 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ConnectManager(IContainerProvider container) : base(container)
        {
        }

        #region Secs协议操作方法

        /// <summary>
        /// 建立Secs通讯连接 (Equipment -> Host)
        /// </summary>
        public async Task EstablishComm_EtoH()
        {
            await SecsGemRuntime.SendItem("S1F13", true).ConfigureAwait(false);
        }

        /// <summary>
        /// 设备上线处理 (Equipment -> Host)
        /// </summary>
        public async Task Online_EtoH()
        {
            await SecsGemRuntime.SendItem("S1F17", true).ConfigureAwait(false);
            StatusManager.UpdateSEMIMode(StatusManager.Mode_VIDName, ESecsMode.Online_Remote);
            await SendItemAsync("Device Online");
        }

        /// <summary>
        /// 设备离线处理 (Equipment -> Host)
        /// </summary>
        public async Task Offline_EtoH()
        {
            await SecsGemRuntime.SendItem("S1F15", true).ConfigureAwait(false);
            StatusManager.UpdateSEMIMode(StatusManager.Mode_VIDName, ESecsMode.Offline);
           await SendItemAsync("Device Offline");
        }

        /// <summary>
        /// 同时等待设备建立连接，在线，离线事件 一直循环等待
        /// </summary>
        public async Task WaitOnlineAndOfflineAsync(CancellationToken token = default)
        {
            while (!token.IsCancellationRequested)
            {
                using (var cts = new CancellationTokenSource())
                {
                    var estTask = EstablishComm_HtoE(cts.Token);
                    var onlineTask = Online_HtoE(cts.Token);
                    var offlineTask = Offline_HtoE(cts.Token);
                    var completedTask = await Task.WhenAny(estTask, onlineTask, offlineTask).ConfigureAwait(false);
                    cts.Cancel();
                }
            }
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 建立Secs通讯连接 (Host -> Equipment)
        /// </summary>
        private async Task EstablishComm_HtoE(CancellationToken token = default, int waitTime = -1)
        {
            await EventHelper.WaitForEventAsync("S1F13", waitTime, token).ConfigureAwait(false);
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 Online_HtoE。
        /// 正文中的关键协作调用包括 ConfigureAwait、WaitForEventAsync、UpdateSEMIMode、SendItemAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        private async Task Online_HtoE(CancellationToken token = default, int waitTime = -1)
        {
            await EventHelper.WaitForEventAsync("S1F17", waitTime, token).ConfigureAwait(false);
            StatusManager.UpdateSEMIMode(StatusManager.Mode_VIDName, ESecsMode.Online_Remote);
            await SendItemAsync("Device Online");
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Offline_HtoE。
        /// 正文中的关键协作调用包括 ConfigureAwait、WaitForEventAsync、UpdateSEMIMode、SendItemAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        private async Task Offline_HtoE(CancellationToken token = default, int waitTime = -1)
        {
            await EventHelper.WaitForEventAsync("S1F15", waitTime, token).ConfigureAwait(false);
            StatusManager.UpdateSEMIMode(StatusManager.Mode_VIDName, ESecsMode.Offline);
            await SendItemAsync("Device Offline");
        }

        /// <summary>
        /// 发送CEID
        /// </summary>
        /// <param name="CEIDName"></param>
        /// <returns></returns>
        private async Task SendItemAsync(string CEIDName)
        {
            var ceid = VIDManager.CEID.GetDatas().FirstOrDefault(x => x.Name == CEIDName);
            if (ceid != null)
            {
                S6F11Info input = new S6F11Info() { CEID = ceid };
                await SecsGemRuntime.SendItem("S6F11", true, input) ;
            }
        }
        #endregion
    }
}
