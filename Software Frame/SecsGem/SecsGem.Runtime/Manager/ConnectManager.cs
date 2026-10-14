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
using SecsGem.Runtime.Order.S1;
using SecsGem.Runtime.Order.S6;
using SecsGem.Runtime.Variable;
using Secs4Net;
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
        /// 操作员侧进 Online_Local：改 SEMI 模式 + 上报控制状态变更事件。
        /// <para>
        /// 对应迁移表 #1（Offline + 操作员选择上线）与 #9（Remote + 操作员选择 Local 抢回控制权）——
        /// 两种起点都直达 Local，不绕路。
        /// </para>
        /// <para>
        /// 按 §10.2 靠 S6F11 状态变更事件通知 Host，<b>不发 S1F17</b>（Host→Equipment）。
        /// 原实现在这里先发 S1F17，而 <c>S1F17.GetSendItem</c> 恒返回 null，引擎抛"组包失败"。
        /// </para>
        /// </summary>
        public async Task Online_Local_EtoH()
        {
            StatusManager.UpdateSEMIMode(StatusManager.Mode_VIDName, ESecsMode.Online_Local);
            await SendControlStateChangedAsync(ESecsMode.Online_Local);
        }

        /// <summary>
        /// 操作员侧进 Online_Remote：改 SEMI 模式 + 上报控制状态变更。
        /// 标准里 Local→Remote 只给 Host 的 S1F17（迁移表 #7），这里同时给操作员一个直达入口。
        /// </summary>
        public async Task Online_Remote_EtoH()
        {
            StatusManager.UpdateSEMIMode(StatusManager.Mode_VIDName, ESecsMode.Online_Remote);
            await SendControlStateChangedAsync(ESecsMode.Online_Remote);
        }

        /// <summary>
        /// 设备离线处理 (Equipment -> Host)
        /// <para>
        /// 同上：改模式 + 上报控制状态变更，不发 S1F15。
        /// 这也是 <see cref="ShutdownAsync"/> 在断连前调用的那一步。
        /// </para>
        /// </summary>
        public async Task Offline_EtoH()
        {
            StatusManager.UpdateSEMIMode(StatusManager.Mode_VIDName, ESecsMode.Offline);
            await SendControlStateChangedAsync(ESecsMode.Offline);
        }

        /// <summary>
        /// Host 主动控制消息的监听循环：S1F13 / S1F17 / S1F15 谁先到就处理谁，处理完重新排队。
        /// <para>
        /// 这与 <see cref="Online_EtoH"/> 等「操作员侧」入口是两条互补的路，不是重复：
        /// 引擎 <c>ReceiveAsync</c> 负责回 S1F14/S1F18/S1F16（<c>GetReplyItem</c> 只给 ack 字节），
        /// 随后 <c>EventHelper.TriggerEvent(order)</c> 触发事件，这里收到后再切状态并上报 S6F11。
        /// 收发分离，且状态切换点只有这一处。
        /// </para>
        /// </summary>
        /// <param name="token">取消令牌；由 <c>SecsGemRuntime</c> 在连接建立时启动、释放时取消。</param>
        public async Task WaitOnlineAndOfflineAsync(CancellationToken token = default)
        {
            while (!token.IsCancellationRequested)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                var waiters = new[]
                {
                    EstablishComm_HtoE(cts.Token),
                    Online_HtoE(cts.Token),
                    Offline_HtoE(cts.Token)
                };
                try
                {
                    await Task.WhenAny(waiters).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // 必须隔离：处理过程中抛的异常（比如 S6F11 等 S6F12 超时抛 SecsException）
                    // 一旦冲出这个循环，整个监听就此结束，之后 Host 再发 S1F13/S1F17/S1F15
                    // 全部没人处理，表现为"连不上线/下线收不到"，且没有任何报错。
                    System.Diagnostics.Debug.WriteLine($"控制消息处理异常（监听继续）: {ex.Message}");
                    RaiseErrorPublic($"控制消息处理异常: {ex.Message}");
                }
                finally
                {
                    cts.Cancel();
                    // 被取消的两个等待者必须观测，否则 OperationCanceledException 变成 unobserved task exception
                    try { await Task.WhenAll(waiters).ConfigureAwait(false); }
                    catch (OperationCanceledException) { /* 其余等待者随本轮结束，正常 */ }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"控制消息等待者收尾异常: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 供监听循环把处理异常透给上层（原本只有 SecsGemRuntime 内部能用）。
        /// </summary>
        private void RaiseErrorPublic(string message) =>
            SecsGemRuntime?.RaiseErrorPublic(message);

        #endregion

        #region 私有方法

        /// <summary>
        /// 建立通信确认占位：S1F13 由设备发出、S1F14 经 secs4net 内部的应答匹配直接回到发送处，
        /// 偶数 F 的报文不会进主消息通道，所以这里等什么事件都等不到，这个等待者实际是挂起的。
        /// 无害：<see cref="WaitOnlineAndOfflineAsync"/> 用 WhenAny，其余两条不受影响。
        /// 通信建立本身没有需要上报的状态变化（模式仍是 Offline），故不上报 CEID。
        /// </summary>
        private async Task EstablishComm_HtoE(CancellationToken token = default, int waitTime = -1)
        {
            await EventHelper.WaitForEventAsync("S1F13", waitTime, token).ConfigureAwait(false);
        }

        /// <summary>
        /// 上线处理 (Host -> Equipment)：收到 S1F17 且被接受后切 ON-LINE REMOTE 并上报 Device Online。
        /// <para>
        /// 引擎的事件是「收到」而非「接受」就触发的，被拒（ONLACK=NOT_READY，设备运行中）时
        /// 必须不切状态——否则 Host 收到拒绝、设备却自己上线了。
        /// </para>
        /// </summary>
        private async Task Online_HtoE(CancellationToken token = default, int waitTime = -1)
        {
            var args = await EventHelper.WaitForEventAsync("S1F17", waitTime, token).ConfigureAwait(false);
            if (args.Data is not Item ack) return;
            if (ack.GetMemory<byte>() is not { IsEmpty: false } ackByte) return;
            if (ackByte.Span[0] != (byte)EOnflineResult.Accepted) return;

            StatusManager.UpdateSEMIMode(StatusManager.Mode_VIDName, ESecsMode.Online_Remote);
            NotifyControlStateChanged(ESecsMode.Online_Remote);
        }

        /// <summary>
        /// 下线处理 (Host -> Equipment)：收到 S1F15 后切 OFFLINE 并上报控制状态变更。
        /// OFLACK 只有 Accepted 一值（§6.2 无拒绝码），故无需判 ack。
        /// </summary>
        private async Task Offline_HtoE(CancellationToken token = default, int waitTime = -1)
        {
            await EventHelper.WaitForEventAsync("S1F15", waitTime, token).ConfigureAwait(false);
            StatusManager.UpdateSEMIMode(StatusManager.Mode_VIDName, ESecsMode.Offline);
            NotifyControlStateChanged(ESecsMode.Offline);
        }

        /// <summary>
        /// Host 侧的控制状态上报**不** await。
        /// <para>
        /// S1F18/S1F16 已经由引擎先回出去了（§10.1 要求它们先于 S6F11），所以这里等不等
        /// 不影响顺序。但 S6F11 是 wait=true —— Host 不回 S6F12 就会一直挂到 T3 超时，
        /// 而这些方法正是 <see cref="WaitOnlineAndOfflineAsync"/> 的等待者，一旦被它拖住，
        /// 下一条 S1F15/S1F17 就没有人在听了。改成即发即忘，监听循环立刻回到等待态。
        /// </para>
        /// </summary>
        private void NotifyControlStateChanged(ESecsMode target) => _ = SendControlStateChangedAsync(target);
        /// <summary>
        /// 上报控制状态变更：先报通用的 "Device Mode"，再报目标态专用事件。
        /// <para>
        /// "Device Mode" 的 report 挂了 SVID 102(DeviceMode)，事件自带新的模式值，对应
        /// Control State Model §8 的 ControlStateChange（任何控制状态变化都触发的那一个）。
        /// 专用事件（Device Online / Device Local / Device Offline）让 Host 可以只订阅
        /// 自己关心的目标态，两类都不冲突。
        /// </para>
        /// </summary>
        private async Task SendControlStateChangedAsync(ESecsMode target)
        {
            await SendCeidAsync("Device Mode");
            await SendCeidAsync(target switch
            {
                ESecsMode.Online_Remote => "Device Online",
                ESecsMode.Online_Local => "Device Local",
                _ => "Device Offline"
            });
        }

        /// <summary>按名字取 CEID 并发 S6F11；名字不存在只告警不抛（配置可能没加载）。</summary>
        private async Task SendCeidAsync(string ceidName)
        {
            var ceid = VIDManager.CEID.GetDatas().FirstOrDefault(x => x.Name == ceidName);
            if (ceid == null)
            {
                System.Diagnostics.Debug.WriteLine($"警告: CEID 表缺少 \"{ceidName}\"，该事件未上报");
                return;
            }
            await SecsGemRuntime.SendItem("S6F11", true, new S6F11Info { CEID = ceid });
        }
        #endregion
    }
}
