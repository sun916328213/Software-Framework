// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsGemRuntime。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.Core.Conversion、Extension.Core.Concurrency、Extension.Core.Models、Extension.Imaging.Models、Extension.Automation.Device、Extension.Automation.Secs。导入不代表所有类型都会在运行时加载。
using Extension.Core.Conversion;
using Extension.Core.Concurrency;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using Prism.Ioc;
using SecsGem.Core;
using SecsGem.Runtime;
using SecsGem.Runtime.Manager;
using SecsGem.Runtime.Order.S6;
using SecsGem.Runtime.Order.S9;
using SecsGem.Runtime.Variable;
using SecsGem.Runtime.Variable.Data;
using SecsGem.Runtime.Transport;
using SecsGem.Runtime.DataBinding;
using SecsGem.Runtime.Protocol;
using SecsGem.Runtime.Protocol.Messages.S10;
using MessageRegistry = SecsGem.Runtime.Protocol.Registry.SecsMessageRegistry;
using Secs4Net;
using SecsGem.Runtime.Protocol.Registry;
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
    public partial class SecsGemRuntime
    {
        internal VIDManager VariableManager => _vidManager;
        internal StatusManager StateManager => _statusManager;

        #region 管理器实例
        private readonly IContainerProvider _container;
        private readonly ISecsTransport _transport;
        private readonly SecsDataBinding _dataBinding;
        private readonly SecsProtocolEngine _protocolEngine;
        private readonly VIDManager _vidManager;
        private readonly StatusManager _statusManager;
        private readonly MessageRegistry _messageRegistry;
        private CancellationTokenSource? _controlListenerCts;
        private Task? _controlListener;

        /// <summary>S10 终端消息事件；宿主也可通过 TerminalDisplay 指定显示方式。</summary>
        public event Action<string, string>? TerminalMessageReceived;

        /// <summary>S10 终端消息的显示服务，由 WPF 宿主或其他调用方注入。</summary>
        public ITerminalDisplay TerminalDisplay { get; set; }

        #endregion

        #region 构造函数
        public SecsGemRuntime(IContainerProvider container)
            : this(container, new Secs4NetTransport(container))
        {
        }

        /// <summary>
        /// 初始化 SecsGemRuntime 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof、Resolve、SetConnection、AutoRegisterHandlers；具体条件和先后次序见方法体。
        /// </summary>
        internal SecsGemRuntime(IContainerProvider container, ISecsTransport transport)
        {
            _container = container;
            TerminalDisplay = new TerminalMessageEventDisplay((title, message) => TerminalMessageReceived?.Invoke(title, message));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _statusManager = container.Resolve<StatusManager>();
            _vidManager = container.Resolve<VIDManager>();
            _messageRegistry = container.Resolve<MessageRegistry>();
            _statusManager.SetConnection(_transport.IsConnected);
            _messageRegistry.AutoRegisterHandlers();
            _dataBinding = new SecsDataBinding(_vidManager, _statusManager);
            _protocolEngine = new SecsProtocolEngine(_messageRegistry, _dataBinding, _transport, Send, ReplyAsync, RaiseError);
            _transport.OnReceive += SecsBase_OnReceiveAsync;
            _transport.OnReply += SecsBase_OnReply;
            _transport.OnConnectChanged += SecsControl_OnConnectChanged;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 SecsControl_OnConnectChanged。
        /// 正文中的关键协作调用包括 SetConnection、UpdateSEMIMode；具体条件和先后次序见方法体。
        /// </summary>
        private void SecsControl_OnConnectChanged(object sender, ConnectionState e)
        {
            var status = _statusManager;
            status.SetConnection(e == ConnectionState.Selected);
            if (e != ConnectionState.Selected)
            {
                // 断线仍记录 Offline，待下一次联机再上报。
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
                _vidManager.Load();
                _container.Resolve<DeviceManager>().Load(_vidManager.VIDDic_Name);
                // 表加载完才配名字：StatusManager 靠名字找 VID/CEID，顺序反了等于没配。
                // 这四个名字对应 DataBase.LoadDefaults 的打底项，不另造。
                _statusManager.Config("DeviceStatus", "DeviceMode", "Device Status", "Device Mode");
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
        public ESecsMode Mode => _statusManager.CurrentMode;

        /// <summary>
        /// 当前设备状态
        /// </summary>
        public EDeviceStatus Status => _statusManager.CurrentStatus;

        /// <summary>
        /// 获取设备模式
        /// </summary>
        public ESecsMode GetDeviceMode() => _statusManager.CurrentMode;

        /// <summary>
        /// 获取设备状态
        /// </summary>
        public EDeviceStatus GetDeviceStatus() => _statusManager.CurrentStatus;
        #endregion

        #region 门面API：数据读写（原 VIDManager 对外能力）
        /// <summary>
        /// 更新VID数据（Host 查询 S1F3/S1F11 时返回该值）
        /// </summary>
        /// <param name="pName">VID名称</param>
        /// <param name="pValue">数据</param>
        public void SetVid(string pName, object pValue) => _dataBinding.SetVariable(pName, pValue);

        /// <summary>
        /// 追加VID数据（列表型VID追加一项）
        /// </summary>
        /// <param name="pName">VID名称</param>
        /// <param name="pValue">数据</param>
        public void AppendVid(string pName, object pValue) => _dataBinding.AppendVariable(pName, pValue);

        /// <summary>
        /// 根据名称获取VID信息对象
        /// </summary>
        /// <param name="pName">VID名称</param>
        public VIDInfo GetVid(string pName) => _dataBinding.GetVariable(pName);

        /// <summary>
        /// 根据名称获取VID值字符串
        /// </summary>
        /// <param name="pName">VID名称</param>
        public string GetVidValue(string pName) => _dataBinding.GetVariableValue(pName);
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
        /// 上报采集事件（S6F11），等待 Host 的 S6F12 确认（时限默认 5 秒，见方法体说明）
        /// </summary>
        /// <param name="pCEID">事件对象</param>
        /// <returns>发送是否成功</returns>
        public async Task<bool> S6F11EventReportSendAsync(CEIDInfo pCEID, TimeSpan? replyTimeout = null)
        {
            if (pCEID == null) return false;
            try
            {
                RefreshEquipmentData();
                await SendItem("S6F11", true, new S6F11Info() { CEID = pCEID })
                    .WaitAsync(replyTimeout ?? TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                return true;
            }
            catch (TimeoutException)
            {
                // Host 不回 S6F12 时 secs4net 本来要挂到 T3（默认 45s）才抛，界面点一次按钮干等 45 秒没法用。
                RaiseError("S6F11 已发出，但 Host 未在时限内回 S6F12（该 CEID 可能未被订阅）");
                return false;
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
            var ceid = _vidManager.CEID.GetDatas().FirstOrDefault(x => x.Name == pCEIDName);
            return S6F11EventReportSendAsync(ceid);
        }
        #endregion

        #region 门面API：通信与上下线控制
        /// <summary>
        /// 建立通信（S1F13，等待 Host S1F14 确认）
        /// </summary>
        public Task EstablishCommunicationAsync() => SendItem("S1F13", true);

        /// <summary>
        /// 设备请求进 Online_Local
        /// </summary>
        /// <returns>是否成功</returns>
        public Task<bool> GoOnlineLocalAsync() => GoOnlineAsync(connect => connect.Online_Local_EtoH(), "上线本地");

        /// <summary>
        /// 设备请求进 Online_Remote
        /// </summary>
        /// <returns>是否成功</returns>
        public Task<bool> GoOnlineRemoteAsync() => GoOnlineAsync(connect => connect.Online_Remote_EtoH(), "上线远程");

        /// <summary>上线入口共用的异常收敛：失败只上报错误并返回 false，不外抛。</summary>
        private async Task<bool> GoOnlineAsync(Func<ConnectManager, Task> action, string operation)
        {
            try
            {
                await action(_container.Resolve<ConnectManager>()).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                RaiseError($"{operation}失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 设备请求下线（更新 SEMI 模式 + 上报 Device Offline 事件）
        /// </summary>
        /// <returns>是否成功</returns>
        public async Task<bool> GoOfflineAsync()
        {
            try
            {
                await _container.Resolve<ConnectManager>().Offline_EtoH().ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                RaiseError($"下线失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 关闭前的有序下线（GEM Control State Model §10.2 + HSMS Connection State Diagram）：
        /// 控制状态转 OFFLINE + S6F11 上报 → 断 HSMS/TCP。
        /// <para>
        /// EQ 侧不发 S1F15（Host→Equipment）；关闭时"通知 Host 我下线了"靠 S6F11 状态变更事件。
        /// 该通知是尽力而为：Host 不回 S6F12 时最多等 <paramref name="notifyTimeout"/>，
        /// 超时只告警继续关闭 —— 关闭必须完成，不能被 Host 拖住。
        /// </para>
        /// </summary>
        public async Task ShutdownAsync(TimeSpan notifyTimeout)
        {
            if (IsConnected)
            {
                try
                {
                    // GoOfflineAsync 内部 S6F11 是 wait=true，会等 S6F12；这里必须加上限
                    await GoOfflineAsync().WaitAsync(notifyTimeout).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    RaiseWarning($"下线通知未完成({ex.GetType().Name})，继续关闭连接");
                }
            }
            await CloseAsync().ConfigureAwait(false);
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
            var gemHandler = _gemEventHandler;
            if (gemHandler != null)
            {
                e.Return = await gemHandler.OnRemoteCommandAsync(
                    new SecsGem.Core.SecsGemRemoteCommand(
                        e.RCMD,
                        new System.Collections.Generic.Dictionary<string, string>(e.Params)))
                    .ConfigureAwait(false);
                return;
            }

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
            await _protocolEngine.ReceiveAsync(pSecsMessage).ConfigureAwait(false);
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
            await _protocolEngine.SendItemAsync(pOrder, IsWait, pData).ConfigureAwait(false);
        }

        /// <summary>
        /// 接收回复数据 Host被动->Device
        /// <para>
        /// 注意：偶数 F 的应答经 secs4net 内部的应答匹配直接回到发送处，不经过主消息通道，
        /// 所以这个方法实际不会被调用到（<c>DispatchPrimaryAsync</c> 只处理奇数 F 的主消息）。
        /// 保留空实现，不要在这里加逻辑。
        /// </para>
        /// </summary>
        /// <param name="pSecsMessage">SecsMessage格式数据</param>
        /// <param name="pData">SecsMessage转string数据</param>
        public Task SecsBase_OnReply(SecsMessage pSecsMessage, string pData)
        {
            //不做处理
            return Task.CompletedTask;
        }
        #endregion

    }
}
