using Extension.Core.Concurrency;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using Prism.Ioc;
using Secs.Order.S6;
using Secs.Variable;
using System.Threading;
using System.Threading.Tasks;

namespace Secs.Manager
{
    public class ConnectManager:ManagerBase
    {
        public ConnectManager(IContainerProvider container) : base(container)
        {
        }

        #region Secs协议操作方法

        /// <summary>
        /// 建立Secs通讯连接 (Equipment -> Host)
        /// </summary>
        public async Task EstablishComm_EtoH()
        {
            await SecsControl.SendItem("S1F13", true).ConfigureAwait(false);
        }

        /// <summary>
        /// 设备上线处理 (Equipment -> Host)
        /// </summary>
        public async Task Online_EtoH()
        {
            await SecsControl.SendItem("S1F17", true).ConfigureAwait(false);
            StatusManager.UpdateSEMIMode(StatusManager.Mode_VIDName, ESecsMode.Online_Remote);
            await SendItemAsync("Device Online");
        }

        /// <summary>
        /// 设备离线处理 (Equipment -> Host)
        /// </summary>
        public async Task Offline_EtoH()
        {
            await SecsControl.SendItem("S1F15", true).ConfigureAwait(false);
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

        private async Task Online_HtoE(CancellationToken token = default, int waitTime = -1)
        {
            await EventHelper.WaitForEventAsync("S1F17", waitTime, token).ConfigureAwait(false);
            StatusManager.UpdateSEMIMode(StatusManager.Mode_VIDName, ESecsMode.Online_Remote);
            await SendItemAsync("Device Online");
        }

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
                await SecsControl.SendItem("S6F11", true, input) ;
            }
        }
        #endregion
    }
}
