using Extension.Helper.TaskHelper;
using Extension.Struct;
using Prism.Ioc;
using Secs.Order.S6;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Secs.Manager
{
    /// <summary>
    /// 状态管理器实现
    /// </summary>
    public class StatusManager : ManagerBase
    {
        /// <summary>
        /// 设备状态的VID名称
        /// </summary>
        public string Status_VIDName { get; set; }
        /// <summary>
        /// 设备模式的VID名称
        /// </summary>
        public string Mode_VIDName { get; set; }

        /// <summary>
        /// 设备状态的CEID名称
        /// </summary>
        public string Status_CEIDName { get; set; }
        /// <summary>
        /// 设备模式的CEID名称
        /// </summary>
        public string Mode_CEIDName { get; set; }

        private readonly SemaphoreSlim _semaphore;
        private readonly object _lockObject = new object();
  

        /// <summary>
        /// 当前设备状态
        /// </summary>
        public EDeviceStatus CurrentStatus { get; private set; } = EDeviceStatus.Idle;

        /// <summary>
        /// 当前SEMI模式
        /// </summary>
        public ESecsMode CurrentMode { get; private set; } = ESecsMode.Offline;

        /// <summary>
        /// 上一次设备状态
        /// </summary>
        private EDeviceStatus LastStatus { get; set; }

        /// <summary>
        /// 上一次SEMI模式
        /// </summary>
        private ESecsMode LastMode { get; set; }

        /// <summary>
        /// 延时时间
        /// </summary>
        public int DelayTime { get; set; } = 200;

        /// <summary>
        /// 构造函数
        /// </summary>
        public StatusManager(IContainerProvider container) :base(container)
        {
            _semaphore = new SemaphoreSlim(2, 2);
        }

        /// <summary>
        /// 配置VID的名字 初次使用必须配置
        /// </summary>
        /// <param name="pVID_Status"></param>
        /// <param name="pVID_Mode"></param>
        /// <param name="pCEID_Status"></param>
        /// <param name="pCEID_Mode"></param>
        public void Config(string pVID_Status, string pVID_Mode, string pCEID_Status, string pCEID_Mode)
        {
            Status_VIDName = pVID_Status;
            Mode_VIDName = pVID_Mode;

            Status_CEIDName = pCEID_Status;
            Mode_CEIDName = pCEID_Mode;
        }

        /// <summary>
        /// 更新SEMI模式。
        /// 修复：模式状态始终写入（断线时也要能写 Offline，否则重连后 Host 拿到错误模式）；
        /// 仅在线时触发 VID 更新与 CEID 事件上报。
        /// </summary>
        /// <param name="pSecsMode">SEMI模式</param>
        /// <exception cref="Exception"></exception>
        public void UpdateSEMIMode(string pVIDName, ESecsMode pSecsMode)
        {
            lock (_lockObject)
            {
                try
                {
                    CurrentMode = pSecsMode;

                    // 离线时只记录状态，不触发事件上报
                    if (!SecsControl.IsConnected) return;

                    if (CurrentMode != LastMode)
                    {
                        Mode_VIDName = pVIDName;
                        EventHelper.TriggerEvent(Mode_VIDName);
                        LastMode = CurrentMode;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"更新SEMI模式异常: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 更新设备状态。门禁策略与 UpdateSEMIMode 相同：状态始终记录，事件仅在线触发。
        /// </summary>
        /// <param name="pStatus">设备状态</param>
        /// <exception cref="Exception"></exception>
        public void UpdateDeviceStatus(string pVIDName, EDeviceStatus pStatus)
        {
            lock (_lockObject)
            {
                try
                {
                    CurrentStatus = pStatus;

                    if (!SecsControl.IsConnected) return;

                    if (CurrentStatus != LastStatus)
                    {
                        Status_VIDName = pVIDName;
                        EventHelper.TriggerEvent(Status_VIDName);
                        LastStatus = CurrentStatus;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"更新设备状态异常: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 启动状态监控
        /// </summary>
        /// <param name="secsControl">Secs控制器</param>
        /// <param name="token">取消令牌</param>
        public async Task StartMonitoringAsync(CancellationToken token = default)
        {

            // 并行启动状态和模式监控
            var statusTask = StatusChangedAsync(token);
            var modeTask = SEMIModeChangedAsync(token);

            await Task.WhenAll(statusTask, modeTask);
        }

        /// <summary>
        /// 设备状态切换
        /// </summary>
        /// <param name="secsControl">Semi控制类</param>
        /// <param name="token">令牌</param>
        /// <returns></returns>
        private async Task StatusChangedAsync(CancellationToken token = default)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _semaphore.WaitAsync(token).ConfigureAwait(false);

                    await EventHelper.WaitForEventAsync(Status_VIDName, -1);
                    VIDManager.UpdateVID(Status_VIDName, CurrentStatus);
        
                    var ceid = VIDManager.CEID.GetDatas().FirstOrDefault(x => x.Name == Status_CEIDName);
                    if (ceid != null)
                    {
                        S6F11Info input = new S6F11Info() { CEID = ceid };
                        await SecsControl.SendItem("S6F11", true, input).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消操作，不需要记录错误
                    break;
                }
                catch (Exception ex)
                {
                    // 监控循环内单次失败只记录不中断，保证监控线程存活
                    SecsControl?.RaiseErrorPublic($"状态切换异常: {ex.Message}");
                }
                finally
                {
                    _semaphore.Release();
                    await Task.Delay(DelayTime, token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 设备通讯模式切换
        /// </summary>
        /// <param name="secsControl">Semi控制类</param>
        /// <param name="token">令牌</param>
        /// <returns></returns>
        private async Task SEMIModeChangedAsync(CancellationToken token = default)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _semaphore.WaitAsync(token).ConfigureAwait(false);

                    await EventHelper.WaitForEventAsync(Mode_VIDName, -1);

                    VIDManager.UpdateVID(Mode_VIDName, CurrentMode);
                  
                    var ceid = VIDManager.CEID.GetDatas().FirstOrDefault(x => x.Name == Mode_CEIDName);
                    if (ceid != null)
                    {
                        S6F11Info input = new S6F11Info() { CEID = ceid };
                        await SecsControl.SendItem("S6F11", true, input).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消操作，不需要记录错误
                    break;
                }
                catch (Exception ex)
                {
                    // 监控循环内单次失败只记录不中断，保证监控线程存活
                    SecsControl?.RaiseErrorPublic($"SEMI模式切换异常: {ex.Message}");
                }
                finally
                {
                    _semaphore.Release();
                    await Task.Delay(DelayTime, token).ConfigureAwait(false);
                }
            }
        }
    }
}
