using Extension.Core.Collections;
using DataManager.Log;
using Microsoft.Extensions.Options;
using Prism.Ioc;
using Secs.Variable;
using Secs4Net;
using Secs4Net.Sml;
using System;
using System.Threading;
using System.Threading.Tasks;
using ConnectionState = Secs4Net.ConnectionState;
using DataManager.Log.SeriLog;

namespace Secs
{
    /// <summary>
    /// Secs事件处理委托
    /// </summary>
    /// <param name="pSecsMessage">Secs消息</param>
    /// <param name="pData">数据</param>
    /// <returns>异步任务</returns>
    public delegate Task SecsHandle(SecsMessage pSecsMessage, string pData);

    /// <summary>
    /// Secs级别事件处理委托
    /// </summary>
    /// <param name="pData">数据</param>
    public delegate void SecsLevelHandle(string pData);

    /// <summary>
    /// Secs/GEM通信助手类 - 提供与Secs/GEM设备的通信功能
    /// 接收循环串行分发：收到主消息直接回包（O(1)，无轮询队列），异常单条隔离。
    /// 实现ISecsGemLogger接口，支持日志记录；实现IDisposable模式，确保资源正确释放。
    /// </summary>
    public class SecsHelper : ISecsGemLogger, IDisposable
    {
        #region 依赖注入属性
        internal LogManager _logManager;
        internal ILog log => _logManager["SEMILog"];
        #endregion

        #region 构造函数
        public SecsHelper(IContainerProvider container)
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
        private SecsGem _secsGem;
        private HsmsConnection _connector;
        private ISecsGemLogger _logger;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// 当前正在处理的主消息包装器。
        /// 接收循环为串行分发，ReplyAsync 直接使用它回包，替代旧的轮询缓存列表（修复并发同S/F回错包问题）。
        /// </summary>
        private PrimaryMessageWrapper _currentPrimary;

        /// <summary>
        /// 配置参数
        /// </summary>
        private IOptions<SecsGemOptions> options { get; set; }
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
        public virtual async Task<bool> ConnectAsync(bool IsActiveMode, string Address, int Port, int DeviceId = 0, int BufferSize = 65535)
        {
            ThrowIfDisposed();

            _logger = this;
            if (IsConnected) return true;

            // 清理现有连接
            _secsGem?.Dispose();
            if (_connector != null)
            {
                await _connector.DisposeAsync().ConfigureAwait(false);
            }

            // Close() 会释放CTS，重连前重建（支持断开后重新连接）
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
            }

            DeviceID = (ushort)DeviceId;

            // 配置连接参数
            options = Options.Create(new SecsGemOptions
            {
                IsActive = IsActiveMode,
                IpAddress = Address,
                Port = Port,
                SocketReceiveBufferSize = BufferSize,
                DeviceId = DeviceID,
            });

            // 初始化连接组件
            _connector = new HsmsConnection(options, _logger);
            _secsGem = new SecsGem(options, _connector, _logger);

            // 绑定连接状态事件
            _connector.ConnectionChanged += OnConnectionChanged;
            // 开启连接
            _connector.Start(_cancellationTokenSource.Token);

            _ = RunPrimaryLoopAsync();
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
        /// 异步关闭连接
        /// </summary>
        /// <returns>关闭结果</returns>
        public virtual Task CloseAsync()
        {
            return Task.Run(Close);
        }

        /// <summary>
        /// 同步关闭连接：先取消接收循环，再按序释放资源
        /// </summary>
        public virtual void Close()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }

            // 确保按正确顺序释放资源
            if (_connector != null)
            {
                _connector.ConnectionChanged -= OnConnectionChanged;
                _ = _connector.DisposeAsync();
            }

            _secsGem?.Dispose();

            _currentPrimary = null;
            _secsGem = null;
            IsConnected = false;
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

            var enumerator = _secsGem.GetPrimaryMessageAsync().GetAsyncEnumerator(_cancellationTokenSource.Token);
            try
            {
                while (await enumerator.MoveNextAsync().ConfigureAwait(false) &&
                       !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var wrapper = enumerator.Current;
                    if (wrapper == null) continue;

                    await DispatchPrimaryAsync(wrapper).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不需要记录
            }
            catch (Exception ex)
            {
                RaiseError($"接收主消息循环异常: {ex.Message}");
            }
            finally
            {
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
                    await handler.Invoke(msg, sml).ConfigureAwait(false);
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
            OnConnectChanged?.Invoke(obj, e);
            IsConnected = e == ConnectionState.Selected;
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
                        _ = System.Threading.Tasks.Task.Run(async () =>
                        {
                            try { await handler.Invoke(msg, data).ConfigureAwait(false); }
                            catch (Exception ex) { RaiseError($"OnReply处理异常: {ex.Message}"); }
                        });
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

        public void Info(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            RaiseInfo(msg);
            log?.Add(msg);
        }

        public void Warning(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            RaiseWarning(msg);
            log?.Add(msg);
        }

        public void Error(string msg, SecsMessage message, Exception ex)
        {
            if (string.IsNullOrEmpty(msg)) return;
            RaiseError(msg);
            log?.Add(msg);
        }

        public void Error(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            RaiseError(msg);
            log?.Add(msg);
        }

        public void Error(string msg, Exception ex)
        {
            if (string.IsNullOrEmpty(msg)) return;
            RaiseError(msg);
            log?.Add(msg);
        }

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
                        Close();

                        // 释放CancellationTokenSource放在Close之后（Close中还要用Token取消循环）
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
