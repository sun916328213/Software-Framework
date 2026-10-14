// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：Secs4NetTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.Core.Collections、DataManager.Core.Log、DataManager.Runtime.Log、Microsoft.Extensions.Options、Prism.Ioc、SecsGem.Runtime.Variable。导入不代表所有类型都会在运行时加载。
using Extension.Core.Collections;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using Microsoft.Extensions.Options;
using Prism.Ioc;
using SecsGem.Runtime.Variable;
using Secs4Net;
using Secs4Net.Sml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConnectionState = Secs4Net.ConnectionState;
using DataManager.Runtime.Log.SeriLog;
using SecsGem.Core;
using SecsGem.Runtime;
using SecsGem.Runtime.Transport;

namespace SecsGem.Runtime.Transport
{
    /// <summary>
    /// Secs/GEM通信助手类 - 提供与Secs/GEM设备的通信功能
    /// 接收循环串行分发：收到主消息直接回包（O(1)，无轮询队列），异常单条隔离。
    /// 实现ISecsGemLogger接口，支持日志记录；实现IDisposable模式，确保资源正确释放。
    /// </summary>
    public class Secs4NetTransport : ISecsTransport, ISecsGemLogger
    {
        #region 依赖注入属性
        internal LogManager _logManager;
        internal ILog log => _logManager["SEMILog"];
        #endregion

        #region 构造函数
        public Secs4NetTransport(IContainerProvider container)
        {
            _logManager = container.Resolve<LogManager>();
        }

        #endregion

        #region 自定义事件
        /// <summary>
        /// 连接状态事件
        /// </summary>
        public virtual event EventHandler<ConnectionState> OnConnectChanged;

        /// <summary>
        /// Device发送数据事件
        /// </summary>
        public virtual event SecsHandle OnSend;

        /// <summary>
        /// Host主消息事件(需要设备回复) - 在接收循环内顺序await分发
        /// </summary>
        public virtual event SecsHandle OnReceive;

        /// <summary>
        /// Device收到Host回复数据事件
        /// </summary>
        public virtual event SecsHandle OnReply;

        /// <summary>
        /// OnInfo事件
        /// </summary>
        public virtual event SecsLevelHandle OnInfo;

        /// <summary>
        /// OnWaring事件
        /// </summary>
        public virtual event SecsLevelHandle OnWaring;

        /// <summary>
        /// OnError事件
        /// </summary>
        public virtual event SecsLevelHandle OnError;

        /// <summary>
        /// OnDebug事件
        /// </summary>
        public virtual event SecsLevelHandle OnDebug;
        #endregion

        #region 自定义属性
        /// <summary>
        /// 是否在线
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// 设备ID
        /// </summary>
        public ushort DeviceID { get; set; }

        /// <summary>
        /// ID
        /// </summary>
        public int ID { get; set; } = 0;

        #endregion

        #region SEMI参数
        private Secs4Net.SecsGem _secsGem;
        private HsmsConnection _connector;
        private ISecsGemLogger _logger;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// 当前代际的接收循环任务。Close/Dispose 会 await 它，确保返回时该代际的接收分发已真正结束。
        /// </summary>
        private Task _receiveLoopTask;

        /// <summary>
        /// 关闭完成任务的单一所有者：重复 Close/CloseAsync/Dispose 都返回同一个最终结果（幂等）。
        /// </summary>
        private Task _closeTask;
        private readonly object _closeSync = new object();

        /// <summary>
        /// 当前代际已发出但尚未结束的回复通知任务（OnReply 的即发即忘回调），关闭时一并排空。
        /// </summary>
        private readonly List<Task> _pendingNotifications = new List<Task>();
        private readonly object _notificationSync = new object();

        /// <summary>
        /// 当前正在处理的主消息包装器。
        /// 接收循环为串行分发，ReplyAsync 直接使用它回包，替代旧的轮询缓存列表（修复并发同S/F回错包问题）。
        /// </summary>
        private PrimaryMessageWrapper _currentPrimary;

        /// <summary>
        /// 配置参数
        /// </summary>
        private IOptions<Secs4Net.SecsGemOptions> options { get; set; }
        #endregion

        #region lock对象
        private readonly object obj_in = new object();
        private readonly object obj_out = new object();
        #endregion

        #region 受保护的事件触发方法

        /// <summary>
        /// 触发错误事件
        /// </summary>
        /// <param name="message">错误消息</param>
        protected virtual void RaiseError(string message)
        {
            try { OnError?.Invoke(message); } catch { }
        }

        /// <summary>
        /// 触发信息事件
        /// </summary>
        /// <param name="message">信息内容</param>
        protected virtual void RaiseInfo(string message)
        {
            try { OnInfo?.Invoke(message); } catch { }
        }

        /// <summary>
        /// 触发警告事件
        /// </summary>
        /// <param name="message">警告消息</param>
        protected virtual void RaiseWarning(string message)
        {
            try { OnWaring?.Invoke(message); } catch { }
        }

        /// <summary>
        /// 触发调试事件
        /// </summary>
        /// <param name="message">调试消息</param>
        protected virtual void RaiseDebug(string message)
        {
            try { OnDebug?.Invoke(message); } catch { }
        }

        #endregion

        #region 连接管理方法

        /// <summary>
        /// 异步连接到Secs/GEM设备
        /// </summary>
        /// <param name="IsActiveMode">是否为主动连接模式</param>
        /// <param name="Address">设备IP地址</param>
        /// <param name="Port">连接端口</param>
        /// <param name="DeviceId">设备ID</param>
        /// <param name="BufferSize">接收缓冲区大小</param>
        /// <returns>连接结果</returns>
        /// <summary>
        /// 被动模式下的 DeviceID 归一到 15 位。
        /// 原来这里是无检查的 <c>(ushort)DeviceId</c>：负数会变成 65535、70000 会绕回 4464，
        /// 发出去的报文头带着一个谁都不认的值，Host 直接丢弃。
        /// </summary>
        private static ushort ClampDeviceId(int deviceId) =>
            (ushort)Math.Clamp(deviceId, 0, 32767);

        public virtual async Task<bool> ConnectAsync(bool IsActiveMode, string Address, int Port, int DeviceId = 0, int BufferSize = 65535)
        {
            ThrowIfDisposed();

            _logger = this;
            if (IsConnected) return true;

            // 先等待上一次关闭真正结束，避免旧代际的接收循环/连接释放与新连接重叠
            Task previousClose;
            lock (_closeSync) { previousClose = _closeTask; }
            if (previousClose != null)
            {
                try { await previousClose.ConfigureAwait(false); } catch { }
            }

            // 清理未走 Close 的残留连接
            _secsGem?.Dispose();
            _secsGem = null;
            if (_connector != null)
            {
                var stale = _connector;
                _connector = null;
                stale.ConnectionChanged -= OnConnectionChanged;
                try { await stale.DisposeAsync().ConfigureAwait(false); } catch { }
            }

            // 上一轮 Close 已释放 CTS，这里重建（支持断开后重新连接）
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            // 允许新一轮关闭
            lock (_closeSync) { _closeTask = null; }

            // DeviceID 只在被动模式下有意义。主动模式下标准规定报文头恒为 0，
            // 而且 secs4net 收报文时会按 header.DeviceId != DeviceId 丢弃不匹配的报文
            // （SecsGem.cs:149）—— 留着用户填的值，Host 发来的消息会被静默丢掉。
            DeviceID = IsActiveMode ? (ushort)0 : ClampDeviceId(DeviceId);

            // 配置连接参数
            options = Options.Create(new Secs4Net.SecsGemOptions
            {
                IsActive = IsActiveMode,
                IpAddress = Address,
                Port = Port,
                SocketReceiveBufferSize = BufferSize,
                DeviceId = DeviceID,
            });

            // 初始化连接组件
            _connector = new HsmsConnection(options, _logger);
            _secsGem = new Secs4Net.SecsGem(options, _connector, _logger);

            // 记录生效的连接身份：排查"Host 发来的报文收不到"时先看这一行。
            // secs4net 收报文会按 header.DeviceId != DeviceId 静默丢弃（SecsGem.cs:149），
            // 被丢掉的消息连 trace 都不会有，所以这里必须把实际用的值写下来。
            log?.Add(IsActiveMode
                ? $"[HSMS] 主动模式：设备拨号连 {Address}:{Port}（要求 Host 在该端口监听）DeviceID=0（忽略配置值 {DeviceId}）"
                : $"[HSMS] 被动模式：设备在 {Address}:{Port} 监听，等 Host 拨进来（端口被占用会直接绑定失败）DeviceID={DeviceID}");

            // 绑定连接状态事件
            _connector.ConnectionChanged += OnConnectionChanged;
            // 开启连接
            _connector.Start(_cancellationTokenSource.Token);

            // 持有接收循环任务，关闭时可等待它真正结束
            _receiveLoopTask = RunPrimaryLoopAsync();
            return true;
        }

        /// <summary>
        /// 向后兼容的同步连接方法
        /// </summary>
        public virtual bool Connect(bool IsActiveMode, string Address, int Port, int DeviceId = 0, int BufferSize = 65535)
        {
            return Task.Run(async () => await ConnectAsync(IsActiveMode, Address, Port, DeviceId, BufferSize).ConfigureAwait(false))
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// 连接（参数从 Config//SecsJson/SecsParam.json 读取）
        /// </summary>
        /// <returns></returns>
        public virtual bool Connect()
        {
            SecsParam s = new SecsParam();
            return Connect(s._SecsParam.IsActiveMode, s._SecsParam.IpAddress, s._SecsParam.Port, s._SecsParam.DeviceId, s._SecsParam.BufferSize);
        }

        /// <summary>
        /// 异步关闭连接：取消接收循环 → await 接收循环结束 → await 连接释放 → 释放 CTS。
        /// 幂等：重复调用返回同一个最终完成 Task；该 Task 完成即代表当前代际已结束。
        /// </summary>
        /// <returns>关闭完成结果</returns>
        public virtual Task CloseAsync()
        {
            lock (_closeSync)
            {
                // Task.Run 保证 _closeTask 先落位再执行，避免同步重入产生第二个关闭流程
                return _closeTask ??= Task.Run(CloseCoreAsync);
            }
        }

        /// <summary>
        /// 同步关闭连接（向后兼容入口）：阻塞等待 CloseAsync 的同一最终结果。
        /// </summary>
        public virtual void Close()
        {
            try
            {
                CloseAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                RaiseError($"关闭连接异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭实现：不丢弃任何异步释放任务，也不提前释放仍在使用的 CTS。
        /// 接收循环结束与连接释放都被 await，返回后不再有当前代际的接收/回复任务。
        /// </summary>
        private async Task CloseCoreAsync()
        {
            var wasConnected = IsConnected;

            // 1) 请求取消（此时接收循环仍在使用 CTS，不能释放）
            try { _cancellationTokenSource?.Cancel(); }
            catch (ObjectDisposedException) { }

            // 2) 等待当前代际接收循环真正结束（返回后不再有该代际的接收分发）
            var loop = _receiveLoopTask;
            _receiveLoopTask = null;
            if (loop != null)
            {
                try { await loop.ConfigureAwait(false); } catch { }
            }

            // 3) 解绑并 await 连接释放（不再丢弃 DisposeAsync 的返回值）
            var connector = _connector;
            _connector = null;
            if (connector != null)
            {
                connector.ConnectionChanged -= OnConnectionChanged;
                try { await connector.DisposeAsync().ConfigureAwait(false); }
                catch (Exception ex) { RaiseError($"释放HSMS连接异常: {ex.Message}"); }
            }

            _secsGem?.Dispose();
            _secsGem = null;
            _currentPrimary = null;
            IsConnected = false;

            // 4) 排空当前代际已发出的回复通知任务（返回后不再有晚回写）
            await DrainNotificationsAsync().ConfigureAwait(false);

            if (wasConnected)
            {
                OnConnectChanged?.Invoke(this, ConnectionState.Retry);
            }

            // 5) 接收循环已结束，安全释放 CTS（避免早释放导致接收循环读到已释放对象）
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        /// <summary>
        /// 记录一个即发即忘的回复通知任务，供关闭时排空。
        /// </summary>
        private void TrackNotification(Task task)
        {
            if (task == null) return;
            lock (_notificationSync)
            {
                _pendingNotifications.RemoveAll(t => t.IsCompleted);
                _pendingNotifications.Add(task);
            }
        }

        /// <summary>
        /// 等待当前代际所有未完成的回复通知任务结束。
        /// </summary>
        private async Task DrainNotificationsAsync()
        {
            Task[] pending;
            lock (_notificationSync)
            {
                pending = _pendingNotifications.Where(t => !t.IsCompleted).ToArray();
                _pendingNotifications.Clear();
            }

            if (pending.Length == 0) return;
            try { await Task.WhenAll(pending).ConfigureAwait(false); }
            catch { /* 单条通知的异常已在其自身内捕获并上报 */ }
        }

        #endregion

        #region 消息处理方法

        /// <summary>
        /// 主消息接收循环：串行分发每条主消息，处理器异常单条隔离不中断循环。
        /// 替代旧"缓存列表+轮询查找"模式（参照 secs4net 官方推荐的 GetPrimaryMessageAsync+TryReplyAsync 直回模式）。
        /// </summary>
        private async Task RunPrimaryLoopAsync()
        {
            if (_secsGem == null) return;

            // 必须在启动时取到本代际的令牌：CloseCoreAsync 会把 _cancellationTokenSource 置 null，
            // 重连又会换成一个新实例，finally 里再读字段会 NRE 或者读到"下一代"的取消状态。
            var token = _cancellationTokenSource.Token;
            var received = 0;
            var enumerator = _secsGem.GetPrimaryMessageAsync().GetAsyncEnumerator(token);
            try
            {
                while (await enumerator.MoveNextAsync().ConfigureAwait(false) &&
                       !token.IsCancellationRequested)
                {
                    var wrapper = enumerator.Current;
                    if (wrapper == null) continue;

                    received++;
                    await DispatchPrimaryAsync(wrapper).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不需要记录
            }
            catch (Exception ex)
            {
                RaiseError($"接收主消息循环异常（共收到 {received} 条）: {ex.Message}");
            }
            finally
            {
                // 通道关闭却没有取消请求 = 接收循环自己死了。之后 Host 再发什么都进不来，
                // 而现象与"根本没连上"完全一样，必须留痕。
                if (!token.IsCancellationRequested)
                    RaiseError($"接收主消息循环已退出（共收到 {received} 条），HSMS 通道关闭且未在重连");

                if (enumerator != null)
                {
                    await enumerator.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 分发单条主消息：
        /// 奇数F=Host主动请求 → await 分发 OnReceive（内部完成回包），异常时兜底 S9F7；
        /// 偶数F=Host对设备主动消息的回复 → 通知 OnReply。
        /// </summary>
        private async Task DispatchPrimaryAsync(PrimaryMessageWrapper wrapper)
        {
            SecsMessage msg = wrapper.PrimaryMessage;
            if (msg == null) return;

            string sml = msg.ToSml();
            log?.Add($"<-- [0x{wrapper.Id:X8}] {sml}\n");

            bool isPrimaryRequest = msg.F % 2 == 1; // 奇数F=Host主动请求，偶数F=Host对设备主动消息的回复
            if (!isPrimaryRequest)
            {
                // Host 回复数据（设备主动发送的消息的应答）
                string message = $"<-- [0x{wrapper.Id:X8}] {sml}\n";
                SafeInvoke(OnReply, msg, message);
                msg.Dispose(); // 数据已通过事件交付，归还池化内存
                return;
            }

            // Host 主消息：设置当前上下文供 ReplyAsync 直回
            _currentPrimary = wrapper;
            try
            {
                var handler = OnReceive;
                if (handler != null)
                {
                    foreach (SecsHandle subscriber in handler.GetInvocationList())
                    {
                        await subscriber(msg, sml).ConfigureAwait(false);
                    }
                }
                else
                {
                    // 无订阅者：直接回 S9F7 兜底，避免 Host 等待 T3 超时
                    await wrapper.TryReplyAsync(null).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // 单条隔离：处理器异常不影响接收循环；未回包则 S9F7 兜底
                RaiseError($"处理主消息 S{msg.S}F{msg.F} 异常: {ex.Message}");
                try
                {
                    await wrapper.TryReplyAsync(null).ConfigureAwait(false); // replyMessage=null 自动触发 S9F7
                }
                catch (Exception inner)
                {
                    RaiseError($"S9F7兜底失败: {inner.Message}");
                }
            }
            finally
            {
                _currentPrimary = null;
            }
        }

        /// <summary>
        /// 同步安全触发事件，订阅者异常不会影响调用方
        /// </summary>
        private void SafeInvoke(SecsHandle handler, SecsMessage msg, string data)
        {
            try { handler?.Invoke(msg, data); }
            catch (Exception ex) { RaiseError($"事件处理异常: {ex.Message}"); }
        }

        /// <summary>
        /// 连接状态变换函数
        /// </summary>
        void OnConnectionChanged(object obj, Secs4Net.ConnectionState e)
        {
            IsConnected = e == ConnectionState.Selected;
            // 链路状态是"收不到任何报文"时唯一能看的东西：secs4net 只在 Select 完成后才把主消息
            // 投进通道，没到 Selected 就一条都收不到，而 DispatchPrimaryAsync 的 trace 也是空的。
            log?.Add($"[HSMS] 链路状态 → {e}{(e == ConnectionState.Selected ? "（已选中，可以收主机报文）" : "")}\n");
            OnConnectChanged?.Invoke(obj, e);
        }

        /// <summary>
        /// 异步发送Secs消息
        /// </summary>
        /// <param name="pOrder">指令格式（如S1F1）</param>
        /// <param name="pItems">数据内容</param>
        /// <param name="IsWait">是否等待回复</param>
        /// <returns>回复消息</returns>
        public virtual async Task<SecsMessage> Send(string pOrder, Item pItems, bool IsWait = false)
        {
            ThrowIfDisposed();

            if (_secsGem == null || _connector == null ||
                _connector.State != Secs4Net.ConnectionState.Selected)
            {
                return null;
            }

            byte order1 = (byte)(Convert.ToInt32(pOrder.Split('S', 'F')[1]));
            byte order2 = (byte)(Convert.ToInt32(pOrder.Split('S', 'F')[2]));
            SecsMessage secsMessage = new SecsMessage(order1, order2, IsWait)
            {
                SecsItem = pItems
            };
            return await _secsGem.SendAsync(secsMessage).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步发送Secs消息
        /// </summary>
        /// <param name="pSecsMessage">SecsMessage格式的数据</param>
        /// <param name="IsWait">是否等待回复</param>
        /// <returns>回复消息</returns>
        public virtual async Task<SecsMessage> Send(SecsMessage pSecsMessage, bool IsWait = false)
        {
            ThrowIfDisposed();

            if (_secsGem == null || _connector == null ||
                _connector.State != Secs4Net.ConnectionState.Selected)
            {
                return null;
            }

            // 修复：原实现构造了 replyMessage 却发送原始消息，IsWait 参数被忽略
            SecsMessage sendMsg = new SecsMessage(pSecsMessage.S, pSecsMessage.F, IsWait)
            {
                SecsItem = pSecsMessage.SecsItem
            };
            return await _secsGem.SendAsync(sendMsg).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步发送SML格式消息
        /// </summary>
        /// <param name="pItems">SML格式的数据集合</param>
        /// <param name="IsWait">是否等待回复</param>
        /// <returns>回复消息</returns>
        public virtual async Task<SecsMessage> Send(string pItems, bool IsWait = false)
        {
            ThrowIfDisposed();

            if (_secsGem == null || _connector == null ||
                _connector.State != Secs4Net.ConnectionState.Selected)
            {
                return null;
            }

            SecsMessage parsed = pItems.ToSecsMessage();
            SecsMessage sendMsg = new SecsMessage(parsed.S, parsed.F, IsWait)
            {
                SecsItem = parsed.SecsItem
            };
            return await _secsGem.SendAsync(sendMsg).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步回复消息（直回模式）：
        /// 使用接收循环注入的当前主消息上下文直接 TryReplyAsync，O(1) 无轮询无竞态。
        /// pItems=null 时由底层自动回 S9F7。
        /// </summary>
        /// <param name="pSecsMessage">接收的原始消息</param>
        /// <param name="pItems">回复数据项</param>
        /// <returns>回复结果</returns>
        public virtual async Task ReplyAsync(SecsMessage pSecsMessage, Item pItems)
        {
            ThrowIfDisposed();

            PrimaryMessageWrapper recv = _currentPrimary;
            if (recv == null)
            {
                throw new TimeoutException("找不到对应的主消息（只能在接收处理上下文中调用 ReplyAsync）");
            }

            SecsMessage replyMessage = null;

            // pItems=null 时传null给TryReplyAsync，底层自动触发S9F7错误指令
            if (pItems != null)
            {
                byte s = pSecsMessage.S;
                byte f = (byte)(pSecsMessage.F + 1);
                replyMessage = new SecsMessage(s, f)
                {
                    SecsItem = pItems
                };
            }

            var replied = await recv.TryReplyAsync(replyMessage).ConfigureAwait(false);

            if (!replied)
            {
                throw new InvalidOperationException("回复消息失败");
            }
        }

        #endregion

        #region 日志记录方法

        /// <summary>
        /// 收到消息日志钩子：
        /// 奇数F主消息的分发由接收循环(RunPrimaryLoopAsync)负责，此处不重复触发 OnReceive；
        /// 偶数F应答消息不会进入主消息通道（底层直接路由给发送等待者），因此在此异步通知 OnReply 订阅者。
        /// </summary>
        public void MessageIn(SecsMessage msg, int systembyte)
        {
            if (msg == null) return;

            lock (obj_in)
            {
                ID = systembyte;
                log?.Add($"<-- [0x{systembyte:X8}] {msg.ToSml()}\n");

                // 偶数F = Host 对设备主动消息的回复：仅通知，不影响协议处理
                if (msg.F % 2 == 0)
                {
                    var handler = OnReply;
                    if (handler != null)
                    {
                        string data = $"[0x{systembyte:X8}] {msg.ToSml()}\n";
                        TrackNotification(System.Threading.Tasks.Task.Run(async () =>
                        {
                            try { await handler.Invoke(msg, data).ConfigureAwait(false); }
                            catch (Exception ex) { RaiseError($"OnReply处理异常: {ex.Message}"); }
                        }));
                    }
                }
            }
        }

        /// <summary>
        /// 发送消息日志（方向 --> ，修复原来收发方向难辨的问题）
        /// </summary>
        public void MessageOut(SecsMessage msg, int systembyte)
        {
            if (msg == null) return;

            lock (obj_out)
            {
                ID = systembyte;
                string message = $"--> [0x{systembyte:X8}] {msg.ToSml()}\n";
                SafeInvoke(OnSend, msg, message);
                log?.Add(message);
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Info。
        /// 正文中的关键协作调用包括 IsNullOrEmpty、RaiseInfo；具体条件和先后次序见方法体。
        /// </summary>
        public void Info(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            RaiseInfo(msg);
            log?.Add(msg);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Warning。
        /// 正文中的关键协作调用包括 IsNullOrEmpty、RaiseWarning；具体条件和先后次序见方法体。
        /// </summary>
        public void Warning(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            RaiseWarning(msg);
            log?.Add(msg);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Error。
        /// 正文中的关键协作调用包括 IsNullOrEmpty、RaiseError；具体条件和先后次序见方法体。
        /// </summary>
        public void Error(string msg, SecsMessage message, Exception ex)
        {
            if (string.IsNullOrEmpty(msg)) return;
            RaiseError(msg);
            log?.Add(msg);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Error。
        /// 正文中的关键协作调用包括 IsNullOrEmpty、RaiseError；具体条件和先后次序见方法体。
        /// </summary>
        public void Error(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            RaiseError(msg);
            log?.Add(msg);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Error。
        /// 正文中的关键协作调用包括 IsNullOrEmpty、RaiseError；具体条件和先后次序见方法体。
        /// </summary>
        public void Error(string msg, Exception ex)
        {
            if (string.IsNullOrEmpty(msg)) return;
            RaiseError(msg);
            log?.Add(msg);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Debug。
        /// 正文中的关键协作调用包括 IsNullOrEmpty、RaiseDebug；具体条件和先后次序见方法体。
        /// </summary>
        public void Debug(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            RaiseDebug(msg);
            log?.Add(msg);
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// 释放资源的具体实现
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        // Close 会等待接收循环与连接释放真正结束，并自行释放 CTS
                        Close();

                        // 兜底：Close 异常路径未走到 CTS 释放时，此处补一次（成功路径下已为 null）
                        _cancellationTokenSource?.Dispose();
                        _cancellationTokenSource = null;
                    }
                    catch (Exception ex)
                    {
                        RaiseError($"释放资源异常: {ex.Message}");
                    }
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 确保对象未被释放
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private bool _disposed = false;

        #endregion
    }
}
