// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：StatusManager。成员说明可结合下方具体实现阅读。
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
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SecsGem.Runtime.StateMachine;

namespace SecsGem.Runtime.Manager
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
        private readonly SecsStateMachine _state = new SecsStateMachine();
  

        /// <summary>
        /// 当前设备状态
        /// </summary>
        public EDeviceStatus CurrentStatus => _state.Status;

        /// <summary>
        /// 当前SEMI模式
        /// </summary>
        public ESecsMode CurrentMode => _state.Mode;

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetConnection。
        /// 正文中的关键协作调用包括 SetConnection；具体条件和先后次序见方法体。
        /// </summary>
        internal void SetConnection(bool connected) => _state.SetConnection(connected);

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
        /// 缺 VID 名保护：<c>Mode_VIDName</c> 只有调过 <see cref="Config"/> 才有值，
        /// 而 TriggerEvent 对空名直接抛"指令名称不能为空"，会把上/下线整个打断。
        /// 未配置时退化成"只记状态不推 VID"，与 DeviceManager.Load 的缺键保护同一口径。
        /// </summary>
        /// <param name="pSecsMode">SEMI模式</param>
        /// <exception cref="Exception"></exception>
        public void UpdateSEMIMode(string pVIDName, ESecsMode pSecsMode)
        {
            lock (_lockObject)
            {
                try
                {
                    if (_state.SetMode(pSecsMode))
                    {
                        if (string.IsNullOrEmpty(pVIDName))
                        {
                            System.Diagnostics.Debug.WriteLine("警告: 未配置模式VID名(SecsGemRuntime.Config 时 StatusManager.Config)，本次只更新模式状态");
                            return;
                        }
                        Mode_VIDName = pVIDName;

                        // 先把值写进 VID，再让调用方发 S6F11：S6F11.GetSendItem 组包时会
                        // UpdateCEID() 读当时的 VID 快照，值没写进去，report 里 DeviceMode
                        // 会因为 `vid.Value != null` 被整条跳过，Host 收不到模式。
                        if (VIDManager.GetVIDByName(Mode_VIDName) is null)
                            System.Diagnostics.Debug.WriteLine($"警告: VID 表没有 \"{Mode_VIDName}\"，模式值无法写入变量");
                        else
                            VIDManager.UpdateVID(Mode_VIDName, CurrentMode);

                        EventHelper.TriggerEvent(Mode_VIDName);
                        _state.MarkModeReported();
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
        /// 缺 VID 名保护同 <see cref="UpdateSEMIMode"/>：未配置时只记状态不推 VID。
        /// </summary>
        /// <param name="pStatus">设备状态</param>
        /// <exception cref="Exception"></exception>
        public void UpdateDeviceStatus(string pVIDName, EDeviceStatus pStatus)
        {
            lock (_lockObject)
            {
                try
                {
                    if (_state.SetStatus(pStatus))
                    {
                        if (string.IsNullOrEmpty(pVIDName))
                        {
                            System.Diagnostics.Debug.WriteLine("警告: 未配置状态VID名，本次只更新设备状态");
                            return;
                        }
                        Status_VIDName = pVIDName;
                        EventHelper.TriggerEvent(Status_VIDName);
                        _state.MarkStatusReported();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"更新设备状态异常: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 启动状态监控。
        /// <para>
        /// shortcut: 无人调用，启动它会让 ConnectManager 的模式上报重复一次 —— 这里的
        /// SEMIModeChangedAsync 与 ConnectManager.SendControlStateChangedAsync 发的是同一条
        /// CEID "Device Mode"。VID 值已在 <see cref="UpdateSEMIMode"/> 内联写入，不再需要轮询。
        /// 要真正启用监控，得先删掉 ConnectManager 那边的 CEID 上报，或给循环加开关。
        /// </para>
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
                        await SecsGemRuntime.SendItem("S6F11", true, input).ConfigureAwait(false);
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
                    SecsGemRuntime?.RaiseErrorPublic($"状态切换异常: {ex.Message}");
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
                        await SecsGemRuntime.SendItem("S6F11", true, input).ConfigureAwait(false);
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
                    SecsGemRuntime?.RaiseErrorPublic($"SEMI模式切换异常: {ex.Message}");
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
