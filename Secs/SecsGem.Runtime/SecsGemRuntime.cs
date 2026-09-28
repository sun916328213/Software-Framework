using Extension.Core.Conversion;
using Extension.Core.Concurrency;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using Prism.Ioc;
using Secs;
using Secs.Manager;
using Secs.Order.S10.Presentation;
using Secs.Order.S6;
using Secs.Order.S9;
using Secs.Variable;
using Secs.Variable.Data;
using Secs4Net;
using SemiTool.Tool;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SecsGem.Runtime
{
    /// <summary>
    /// Secs/GEM 对外唯一入口（门面）。
    /// 连接、数据读写、报警/事件上报、上下线控制、远程命令回调全部收敛于本类；
    /// 各 Manager 为内部实现细节，业务代码不要再直接访问。
    /// </summary>
    public class SecsGemRuntime : SecsHelper
    {
        #region 管理器实例
        private readonly IContainerProvider _container;

        /// <summary>S10 终端消息的显示服务；宿主可注入 WPF 或其他实现。</summary>
        public ITerminalDisplay TerminalDisplay { get; set; } = new WpfTerminalDisplay();

        /// <summary>
        /// VID 管理实例
        /// </summary>
        [Obsolete("内部实现将收口，请改用 SetVid/GetVid 等门面方法", false)]
        public VIDManager _VIDManager => _container.Resolve<VIDManager>();

        /// <summary>
        /// 状态管理器实例 使用时必须配置
        /// </summary>
        [Obsolete("内部实现将收口，请改用 Mode/Status/GoOnlineRemoteAsync 等门面成员", false)]
        public StatusManager _StatusManager => _container.Resolve<StatusManager>();

        /// <summary>
        /// 连接管理器实例
        /// </summary>
        [Obsolete("内部实现将收口，请改用 EstablishCommunicationAsync/GoOnlineRemoteAsync/GoOnlineLocalAsync", false)]
        public ConnectManager _ConnectManager => _container.Resolve<ConnectManager>();

        /// <summary>
        /// Trace管理实例
        /// </summary>
        [Obsolete("内部实现将收口，暂无替代", false)]
        public TraceManager _TraceManager => _container.Resolve<TraceManager>();

        /// <summary>
        /// 设备管理实例
        /// </summary>
        [Obsolete("内部实现将收口，设备信息经 Config 加载", false)]
        public DeviceManager _DeviceManager => _container.Resolve<DeviceManager>();

        /// <summary>
        /// 指令工厂集合
        /// </summary>
        private FactoryCollection _FactoryCollection => _container.Resolve<FactoryCollection>();
        #endregion

        #region 构造函数
        public SecsGemRuntime(IContainerProvider container) : base(container)
        {
            _container = container;
            _FactoryCollection.AutoRegisterHandlers();
            OnReceive += SecsBase_OnReceiveAsync;
            OnReply += SecsBase_OnReply;
            OnConnectChanged += SecsControl_OnConnectChanged;
        }

        private void SecsControl_OnConnectChanged(object sender, ConnectionState e)
        {
            if (e != ConnectionState.Selected)
            {
                // 断线必须把模式写回 Offline（StatusManager 已修复断线门禁拦截问题）
                var status = _container.Resolve<StatusManager>();
                status.UpdateSEMIMode(status.Mode_VIDName, ESecsMode.Offline);
            }
        }
        #endregion

        #region 初始化方法
        /// <summary>
        /// Semi初始化（加载 VID/CEID/Report 配置）
        /// </summary>
        /// <returns></returns>
        public void Config()
        {
            try
            {
                _VIDManager.Load();
                _DeviceManager.Load(_VIDManager.VIDDic_Name);
            }
            catch (Exception ex)
            {
                throw new Exception($"Secs配置加载失败: {ex.Message}", ex);
            }
        }
        #endregion

        #region 门面API：状态查询
        /// <summary>
        /// 当前SEMI模式
        /// </summary>
        public ESecsMode Mode => _StatusManager.CurrentMode;

        /// <summary>
        /// 当前设备状态
        /// </summary>
        public EDeviceStatus Status => _StatusManager.CurrentStatus;

        /// <summary>
        /// 获取设备模式
        /// </summary>
        public ESecsMode GetDeviceMode() => _StatusManager.CurrentMode;

        /// <summary>
        /// 获取设备状态
        /// </summary>
        public EDeviceStatus GetDeviceStatus() => _StatusManager.CurrentStatus;
        #endregion

        #region 门面API：数据读写（原 VIDManager 对外能力）
        /// <summary>
        /// 更新VID数据（Host 查询 S1F3/S1F11 时返回该值）
        /// </summary>
        /// <param name="pName">VID名称</param>
        /// <param name="pValue">数据</param>
        public void SetVid(string pName, object pValue) => _VIDManager.UpdateVID(pName, pValue);

        /// <summary>
        /// 追加VID数据（列表型VID追加一项）
        /// </summary>
        /// <param name="pName">VID名称</param>
        /// <param name="pValue">数据</param>
        public void AppendVid(string pName, object pValue) => _VIDManager.AppendVID(pName, pValue);

        /// <summary>
        /// 根据名称获取VID信息对象
        /// </summary>
        /// <param name="pName">VID名称</param>
        public VIDInfo GetVid(string pName) => _VIDManager.GetVIDByName(pName);

        /// <summary>
        /// 根据名称获取VID值字符串
        /// </summary>
        /// <param name="pName">VID名称</param>
        public string GetVidValue(string pName) => _VIDManager.GetValueByName(pName);
        #endregion

        #region 门面API：主动上报
        /// <summary>
        /// 上报报警（S5F1），等待 Host 的 S5F2 确认
        /// </summary>
        /// <param name="pAlarm">报警信息</param>
        /// <returns>发送是否成功</returns>
        public async Task<bool> S5F1AlarmReportSendAsync(AlarmInfo pAlarm)
        {
            if (pAlarm == null) return false;
            try
            {
                await SendItem("S5F1", true, pAlarm).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                RaiseError($"S5F1报警上报失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 上报采集事件（S6F11），等待 Host 的 S6F12 确认
        /// </summary>
        /// <param name="pCEID">事件对象</param>
        /// <returns>发送是否成功</returns>
        public async Task<bool> S6F11EventReportSendAsync(CEIDInfo pCEID)
        {
            if (pCEID == null) return false;
            try
            {
                await SendItem("S6F11", true, new S6F11Info() { CEID = pCEID }).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                RaiseError($"S6F11事件上报失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 按CEID名称上报采集事件（S6F11）
        /// </summary>
        /// <param name="pCEIDName">CEID名称</param>
        /// <returns>发送是否成功</returns>
        public Task<bool> S6F11EventReportSendAsync(string pCEIDName)
        {
            if (string.IsNullOrEmpty(pCEIDName)) return Task.FromResult(false);
            var ceid = _VIDManager.CEID.GetDatas().FirstOrDefault(x => x.Name == pCEIDName);
            return S6F11EventReportSendAsync(ceid);
        }

        /// <summary>
        /// 上报报警（旧名兼容）
        /// </summary>
        /// <param name="pAlarms">报警信息</param>
        [Obsolete("请改用 S5F1AlarmReportSendAsync", false)]
        public async void UpdateAlarm(AlarmInfo pAlarms)
        {
            await S5F1AlarmReportSendAsync(pAlarms);
        }
        #endregion

        #region 门面API：通信与上下线控制
        /// <summary>
        /// 建立通信（S1F13，等待 Host S1F14 确认）
        /// </summary>
        public Task EstablishCommunicationAsync() => SendItem("S1F13", true);

        /// <summary>
        /// 设备请求上线远程控制（S1F17 + 更新模式 + 上报 Device Online 事件）
        /// </summary>
        /// <returns>是否成功</returns>
        public async Task<bool> GoOnlineRemoteAsync()
        {
            try
            {
                await _ConnectManager.Online_EtoH().ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                RaiseError($"上线远程失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 设备请求下线（S1F15 + 更新模式 + 上报 Device Offline 事件）
        /// </summary>
        /// <returns>是否成功</returns>
        public async Task<bool> GoOfflineAsync()
        {
            try
            {
                await _ConnectManager.Offline_EtoH().ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                RaiseError($"下线失败: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region 门面API：入站回调注册
        private ISecsControlEvents _events;

        /// <summary>
        /// 供内部组件上报错误（触发 OnError 事件）
        /// </summary>
        public void RaiseErrorPublic(string pMessage) => RaiseError(pMessage);

        /// <summary>
        /// 注册入站事件回调（如 Host 远程命令 S2F41），替代旧的 ActiveReceive 轮询。
        /// </summary>
        /// <param name="pEvents">业务方实现</param>
        public void SetEvents(ISecsControlEvents pEvents)
        {
            _events = pEvents;
        }

        /// <summary>
        /// 由 S2F41 处理器调用，触发业务方远程命令回调
        /// </summary>
        internal async Task RaiseRemoteCommandAsync(RemoteCommandArgs e)
        {
            var handler = _events;
            if (handler == null)
            {
                RaiseWarning($"收到远程命令 {e.RCMD} 但未注册 ISecsControlEvents，已按接受处理");
                return;
            }
            await handler.OnRemoteCommandAsync(e).ConfigureAwait(false);
        }
        #endregion

        #region 消息处理方法
        /// <summary>
        /// 接收数据 Host主动->Device
        /// </summary>
        /// <param name="pSecsMessage">SecsMessage格式数据</param>
        /// <param name="pData">SecsMessage转string数据</param>
        public async Task SecsBase_OnReceiveAsync(SecsMessage pSecsMessage, string pData)
        {
            if (pSecsMessage == null) return;

            string order = "S" + pSecsMessage.S + "F" + pSecsMessage.F;

            // 从缓存字典获取处理器（O(1)，无反射）
            var handler = _FactoryCollection.GetHandler(order);
            if (handler == null)
            {
                // 抛出后由接收循环捕获并自动回 S9Fx 兜底，Host 不必等 T3 超时
                throw new Exception($"未找到对应的处理器: {order}");
            }

            // 异步回包入口（支持需要回调业务的指令）；返回 null 时底层自动回 S9F7
            Item item = await handler.GetReplyItemAsync(pSecsMessage).ConfigureAwait(false);
            await ReplyAsync(pSecsMessage, item).ConfigureAwait(false);

            // 触发字典中对应指令的事件（供 EventHelper.WaitForEventAsync 使用）
            EventHelper.TriggerEvent(order);
        }

        /// <summary>
        /// 发送数据 Device->Host
        /// </summary>
        /// <param name="pOrder">指令</param>
        /// <param name="IsWait">是否等待回复</param>
        /// <param name="pData">发送数据</param>
        /// <returns></returns>
        public virtual async Task SendItem(string pOrder, bool IsWait, params object[] pData)
        {
            if (string.IsNullOrEmpty(pOrder)) return;

            var handler = _FactoryCollection.GetHandler(pOrder);
            if (handler == null)
            {
                throw new Exception($"未找到对应的处理器: {pOrder}");
            }

            Item item;

            switch (pOrder)
            {
                case "S6F11":
                    item = handler.GetSendItem(pData, _VIDManager);
                    break;
                default:
                    item = handler.GetSendItem(pData);
                    break;
            }

            // 组包失败禁止发出空消息（原实现会发出 SecsItem=null 的畸形消息）
            if (item == null)
            {
                throw new Exception($"{pOrder} 组包失败，已取消发送");
            }

            try
            {
                await Send(pOrder, item, IsWait).ConfigureAwait(false);
            }
            catch (SecsException ex)
            {
                if (ex.Message.Contains("T3 Timeout"))   ///T3对话超时
                {
                    SecsMessage secsMessage = ex.SecsMessage;
                    SS9F9 data = new SS9F9() { SecsMessage = secsMessage, DeviceID = DeviceID, ID = ID };
                    await SendItem("S9F9", false, data).ConfigureAwait(false);
                    throw new Exception($"T3超时: {ex.Message}", ex);
                }
                // 使用统一的异常处理机制记录Secs异常
                throw new Exception($"Secs异常: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 接收回复数据 Host被动->Device
        /// </summary>
        /// <param name="pSecsMessage">SecsMessage格式数据</param>
        /// <param name="pData">SecsMessage转string数据</param>
        public Task SecsBase_OnReply(SecsMessage pSecsMessage, string pData)
        {
            //不做处理
            return Task.CompletedTask;
        }
        #endregion

        #region IDisposable Support
        private bool _disposed = false;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                }

                // 调用基类的Dispose方法
                base.Dispose(disposing);
                _disposed = true;
            }
        }
        #endregion
    }
}
