// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsWorkspace、Lease、WorkbenchCommandHandler。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Controls.Models、System.Collections.ObjectModel、System.Windows.Threading、SecsGem.Runtime.Variable、SecsGem.Runtime.Manager、SecsGem.Core。导入不代表所有类型都会在运行时加载。
using SecsGem.Controls.Models;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using SecsGem.Runtime.Variable;
using SecsGem.Runtime.Manager;
using SecsGem.Core;
using SecsGem.Runtime;
using Secs4Net;

namespace SecsGem.Controls.Services;

/// <summary>SECS 页面流程、事件订阅和状态刷新；不拥有注入的运行时。</summary>
public sealed class SecsWorkspace : IDisposable
{
    private readonly SecsGemRuntime _runtime;
    private readonly VIDManager _catalog;
    private readonly Dispatcher _dispatcher;
    private bool _disposed;
    private readonly DispatcherTimer _stateTimer;
    private int _subscriptions;
    public SecsPageModel Model { get; } = new();
    public ObservableCollection<string> Messages => Model.Messages;

    /// <summary>
    /// 初始化 SecsWorkspace 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 FromSeconds、RefreshState、RefreshCatalog；具体条件和先后次序见方法体。
    /// </summary>
    public SecsWorkspace(SecsGemRuntime runtime, VIDManager catalog, Dispatcher dispatcher)
    {
        _runtime = runtime;
        _catalog = catalog;
        _dispatcher = dispatcher;

        _runtime.OnConnectChanged += OnConnectChanged;
        _runtime.OnSend += OnSend;
        _runtime.OnReceive += OnReceive;
        _runtime.OnReply += OnReply;
        _runtime.OnInfo += OnInfo;
        _runtime.OnWaring += OnWarning;
        _runtime.OnError += OnError;
        _stateTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        _stateTimer.Tick += OnStateTick;

        RefreshState();
        RefreshCatalog();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EnableWorkbenchCommandHandler。
    /// 正文中的关键协作调用包括 SetEventHandler；具体条件和先后次序见方法体。
    /// </summary>
    public void EnableWorkbenchCommandHandler() => _runtime.SetEventHandler(new WorkbenchCommandHandler(this));

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Subscribe。
    /// 正文中的关键协作调用包括 ThrowIf、RefreshState、Start；具体条件和先后次序见方法体。
    /// </summary>
    public IDisposable Subscribe()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (++_subscriptions == 1) { RefreshState(); _stateTimer.Start(); }
        return new Lease(this);
    }
    /// <summary>
    /// Lease 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Lease(SecsWorkspace owner) : IDisposable
    {
        private SecsWorkspace? _owner = owner;
        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Exchange、Stop；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose()
        {
            var workspace = Interlocked.Exchange(ref _owner, null);
            if (workspace != null && --workspace._subscriptions == 0) workspace._stateTimer.Stop();
        }
    }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnStateTick。
    /// 正文中的关键协作调用包括 RefreshState；具体条件和先后次序见方法体。
    /// </summary>
    private void OnStateTick(object? sender, EventArgs e) => RefreshState();
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnConnectChanged。
    /// 正文中的关键协作调用包括 Post、AddLog、ToString、RefreshState；具体条件和先后次序见方法体。
    /// </summary>
    private void OnConnectChanged(object? sender, ConnectionState state) => Post(() => { AddLog("HSMS", state.ToString()); RefreshState(); });
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnSend。
    /// 正文中的关键协作调用包括 LogMessageAsync；具体条件和先后次序见方法体。
    /// </summary>
    private Task OnSend(SecsMessage message, string data) => LogMessageAsync("发送", message, data);
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnReceive。
    /// 正文中的关键协作调用包括 LogMessageAsync；具体条件和先后次序见方法体。
    /// </summary>
    private Task OnReceive(SecsMessage message, string data) => LogMessageAsync("接收", message, data);
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnReply。
    /// 正文中的关键协作调用包括 LogMessageAsync；具体条件和先后次序见方法体。
    /// </summary>
    private Task OnReply(SecsMessage message, string data) => LogMessageAsync("应答", message, data);
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnInfo。
    /// 正文中的关键协作调用包括 Post、AddLog；具体条件和先后次序见方法体。
    /// </summary>
    private void OnInfo(string message) => Post(() => AddLog("信息", message));
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnWarning。
    /// 正文中的关键协作调用包括 Post、AddLog；具体条件和先后次序见方法体。
    /// </summary>
    private void OnWarning(string message) => Post(() => AddLog("警告", message));
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnError。
    /// 正文中的关键协作调用包括 Post、AddLog；具体条件和先后次序见方法体。
    /// </summary>
    private void OnError(string message) => Post(() => AddLog("错误", message));

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 LogMessageAsync。
    /// 正文中的关键协作调用包括 Post、AddLog；具体条件和先后次序见方法体。
    /// </summary>
    private Task LogMessageAsync(string direction, SecsMessage message, string data)
    {
        Post(() => AddLog(direction, $"S{message.S}F{message.F} {data}"));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Post。
    /// 正文中的关键协作调用包括 BeginInvoke、action；具体条件和先后次序见方法体。
    /// </summary>
    private void Post(Action action)
    {
        if (!_disposed && !_dispatcher.HasShutdownStarted)
            _dispatcher.BeginInvoke(() => { if (!_disposed) action(); });
    }

    /// <summary>
    /// 执行添加操作；对应入口为 AddLog。
    /// 正文中的关键协作调用包括 Add、RemoveAt；具体条件和先后次序见方法体。
    /// </summary>
    private void AddLog(string kind, string message)
    {
        if (_disposed) return;
        Messages.Add($"{DateTime.Now:HH:mm:ss.fff} [{kind}] {message}");
        if (Messages.Count > 1000)
            Messages.RemoveAt(0);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RefreshState。
    /// 正文中的关键协作调用包括 GetState、ToString；具体条件和先后次序见方法体。
    /// </summary>
    public void RefreshState()
    {
        if (_disposed) return;
        var state = _runtime.GetState();
        Model.IsConnected = state.IsConnected;
        Model.ConnectionStatus = state.IsConnected ? "HSMS Selected" : "未连接";
        Model.ModeStatus = state.Mode.ToString();
        Model.DeviceStatus = state.EquipmentStatus.ToString();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RefreshCatalog。
    /// 正文中的关键协作调用包括 ToList、Concat、Select、GetDatas；具体条件和先后次序见方法体。
    /// </summary>
    private void RefreshCatalog()
    {
        Model.Variables = _catalog.SVID.GetDatas().Select(x => new VariableRow("SVID", x))
            .Concat(_catalog.DVID.GetDatas().Select(x => new VariableRow("DVID", x)))
            .Concat(_catalog.ECID.GetDatas().Select(x => new VariableRow("ECID", x)))
            .ToList();
        Model.Events = _catalog.CEID.GetDatas();
        Model.Reports = _catalog.ReportID.GetDatas();
        Model.Alarms = _catalog.Alarm.GetDatas();
    }

    /// <summary>
    /// 运行当前处理流程；对应入口为 RunAsync。
    /// 正文中的关键协作调用包括 action、AddLog、RefreshState；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task RunAsync(string operation, Func<Task> action)
    {
        if (_disposed) return;
        try
        {
            await action();
            AddLog("操作", $"{operation} 完成");
        }
        catch (Exception ex)
        {
            AddLog("错误", $"{operation}: {ex.Message}");
        }
        finally
        {
            RefreshState();
        }
    }

    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 RunAsync、Trim、Parse、ConnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task ConnectAsync() =>
        await RunAsync("连接", async () =>
        {
            var options = new SecsGem.Core.SecsGemOptions
            {
                IsActiveMode = Model.ConnectionModeIndex == 0,
                IpAddress = Model.Address.Trim(),
                Port = int.Parse(Model.PortText),
                DeviceId = int.Parse(Model.DeviceIdText)
            };
            if (!await _runtime.ConnectAsync(options))
                throw new InvalidOperationException("连接未建立；请检查对端与端口");
        });

    /// <summary>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task DisconnectAsync() =>
        await RunAsync("断开", _runtime.DisconnectAsync);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EstablishAsync。
    /// 正文中的关键协作调用包括 RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task EstablishAsync() =>
        await RunAsync("S1F13", _runtime.EstablishCommunicationAsync);

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnlineAsync。
    /// 正文中的关键协作调用包括 RunAsync、GoOnlineRemoteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task OnlineAsync() =>
        await RunAsync("Online Remote", async () =>
        {
            if (!await _runtime.GoOnlineRemoteAsync())
                throw new InvalidOperationException("上线失败，查看消息监视器中的错误");
        });

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 OfflineAsync。
    /// 正文中的关键协作调用包括 RunAsync、GoOfflineAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task OfflineAsync() =>
        await RunAsync("Offline", async () =>
        {
            if (!await _runtime.GoOfflineAsync())
                throw new InvalidOperationException("下线失败，查看消息监视器中的错误");
        });

    /// <summary>
    /// 执行加载操作；对应入口为 LoadConfig。
    /// 正文中的关键协作调用包括 Config、RefreshCatalog、AddLog；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public void LoadConfig()
    {
        if (_disposed) return;
        try
        {
            _runtime.Config();
            RefreshCatalog();
            AddLog("配置", "GEM 配置已加载");
        }
        catch (Exception ex)
        {
            AddLog("错误", $"加载配置: {ex.Message}");
        }
    }

    /// <summary>
    /// 执行加载操作；对应入口为 LoadSample。
    /// 正文中的关键协作调用包括 AddLog、Add、RefreshCatalog；具体条件和先后次序见方法体。
    /// </summary>
    public void LoadSample()
    {
        if (_disposed) return;
        if (_catalog.VIDList.Count != 0 || _catalog.CEID.Datas.Count != 0)
        {
            AddLog("提示", "当前已有 GEM 配置，请重启工作台后再载入内存示例");
            return;
        }

        var lot = new VIDInfo { ID = "1", Name = "LotId", Value = "LOT-42", DataType = SecsFormat.ASCII };
        var clock = new VIDInfo { ID = "2", Name = "Clock", Value = "", DataType = SecsFormat.ASCII };
        _catalog.SVID.Datas.Add(lot);
        _catalog.SVID.Datas.Add(clock);
        _catalog.VIDList.Add(lot);
        _catalog.VIDList.Add(clock);
        _catalog.VIDDic_Name[lot.Name] = lot;
        _catalog.VIDDic_Name[clock.Name] = clock;
        _catalog.VIDDic_ID[lot.ID] = lot;
        _catalog.VIDDic_ID[clock.ID] = clock;
        _catalog.ReportID.Datas.Add(new ReportInfo { ID = "7", Name = "LotReport", VID_Collection = "1" });
        _catalog.CEID.Datas.Add(new CEIDInfo { ID = "9", Name = "LotChanged", ReportID_Collection = "7" });
        _catalog.Alarm.Datas.Add(new AlarmInfo { ID = "11", Description = "Workbench sample alarm" });
        RefreshCatalog();
        AddLog("配置", "内存示例已载入；可编辑 LotId、上报 CEID 9 和 ALID 11");
    }

    /// <summary>
    /// 执行发送操作；对应入口为 SendSmlAsync。
    /// 正文中的关键协作调用包括 RunAsync、IsNullOrWhiteSpace、Send、AddLog；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task SendSmlAsync() =>
        await RunAsync("发送 SML", async () =>
        {
            if (!_runtime.IsConnected)
                throw new InvalidOperationException("请先建立 HSMS 连接");
            if (string.IsNullOrWhiteSpace(Model.Sml))
                throw new ArgumentException("请输入 SML 消息");
            var reply = await _runtime.Send(Model.Sml, Model.WaitForReply);
            if (Model.WaitForReply && reply != null)
                AddLog("手工应答", $"S{reply.S}F{reply.F} {reply.SecsItem}");
        });

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetVariable。
    /// 正文中的关键协作调用包括 AddLog、SetVariable、RefreshCatalog；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public void SetVariable()
    {
        if (_disposed) return;
        if (Model.SelectedVariable is not { } selected)
        {
            AddLog("提示", "请先选择变量");
            return;
        }
        try
        {
            _runtime.SetVariable(selected.Name, Model.VariableValue);
            RefreshCatalog();
            AddLog("变量", $"{selected.Name} = {Model.VariableValue}");
        }
        catch (Exception ex)
        {
            AddLog("错误", $"设置变量: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RefreshCatalogNow。
    /// 正文中的关键协作调用包括 RefreshCatalog；具体条件和先后次序见方法体。
    /// </summary>
    public void RefreshCatalogNow() { if (!_disposed) RefreshCatalog(); }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReportEventAsync。
    /// 正文中的关键协作调用包括 RunAsync、ReportEventAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task ReportEventAsync() =>
        await RunAsync("S6F11", async () =>
        {
            if (Model.SelectedEvent is not { } selected)
                throw new InvalidOperationException("请先选择 CEID");
            if (!await _runtime.ReportEventAsync(selected.Name))
                throw new InvalidOperationException("事件上报失败，查看消息监视器中的错误");
        });

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RaiseAlarmAsync。
    /// 正文中的关键协作调用包括 SendAlarmAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task RaiseAlarmAsync() => await SendAlarmAsync(1);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResetAlarmAsync。
    /// 正文中的关键协作调用包括 SendAlarmAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task ResetAlarmAsync() => await SendAlarmAsync(0);

    /// <summary>
    /// 执行发送操作；对应入口为 SendAlarmAsync。
    /// 正文中的关键协作调用包括 RunAsync、ReportAlarmAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    private async Task SendAlarmAsync(byte code) => await RunAsync("S5F1", async () =>
    {
        if (Model.SelectedAlarm is not { } selected)
            throw new InvalidOperationException("请先选择报警");
        if (!await _runtime.ReportAlarmAsync(new SecsGemAlarm(selected.ID, selected.Description, code, selected.Enable)))
            throw new InvalidOperationException("报警上报失败，查看消息监视器中的错误");
    });

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PreviewTerminal。
    /// 正文中的关键协作调用包括 Show；具体条件和先后次序见方法体。
    /// </summary>
    public void PreviewTerminal() { if (!_disposed) _runtime.TerminalDisplay.Show("S10 预览", "Host 终端消息显示预览"); }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ClearLog。
    /// 正文中的关键协作调用包括 Clear；具体条件和先后次序见方法体。
    /// </summary>
    public void ClearLog() { if (!_disposed) Messages.Clear(); }

    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Stop；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Model.IsClosed = true;
        Model.IsConnected = false;
        _stateTimer.Stop();
        _stateTimer.Tick -= OnStateTick;
        _runtime.OnConnectChanged -= OnConnectChanged;
        _runtime.OnSend -= OnSend;
        _runtime.OnReceive -= OnReceive;
        _runtime.OnReply -= OnReply;
        _runtime.OnInfo -= OnInfo;
        _runtime.OnWaring -= OnWarning;
        _runtime.OnError -= OnError;
    }

    /// <summary>
    /// WorkbenchCommandHandler 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：ISecsGemEventHandler。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class WorkbenchCommandHandler(SecsWorkspace viewModel) : ISecsGemEventHandler
    {
        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnRemoteCommandAsync。
        /// 正文中的关键协作调用包括 InvokeAsync、Join、Select、AddLog、TryParse；具体条件和先后次序见方法体。
        /// </summary>
        public Task<byte> OnRemoteCommandAsync(SecsGemRemoteCommand command)
        {
            return viewModel._dispatcher.InvokeAsync(() =>
            {
                var parameters = string.Join(", ", command.Parameters.Select(x => $"{x.Key}={x.Value}"));
                viewModel.AddLog("S2F41", $"{command.Name} {parameters}");
                return byte.TryParse(viewModel.Model.Hcack, out var hcack) ? hcack : (byte)0;
            }).Task;
        }
    }
}
