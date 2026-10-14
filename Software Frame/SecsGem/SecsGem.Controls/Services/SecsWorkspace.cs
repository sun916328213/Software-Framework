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
    public ObservableCollection<LogRow> Messages => Model.Messages;

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
        Messages.Add(new LogRow($"{DateTime.Now:HH:mm:ss.fff}", kind, message));
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
                // 主动模式下这个值不参与通信，但必须能解析出来，否则连不上。
                DeviceId = int.TryParse(Model.DeviceIdText, out var deviceId) ? deviceId : 0
            };
            if (!await _runtime.ConnectAsync(options))
                throw new InvalidOperationException("连接未建立；请检查对端与端口");
        });

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnlineLocalAsync。
    /// 正文中的关键协作调用包括 RunAsync、GoOnlineLocalAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task OnlineLocalAsync() =>
        await RunAsync("上线本地", async () =>
        {
            if (!await _runtime.GoOnlineLocalAsync())
                throw new InvalidOperationException("上线本地失败，查看消息监视器中的错误");
        });

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnlineRemoteAsync。
    /// 正文中的关键协作调用包括 RunAsync、GoOnlineRemoteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task OnlineRemoteAsync() =>
        await RunAsync("上线远程", async () =>
        {
            if (!await _runtime.GoOnlineRemoteAsync())
                throw new InvalidOperationException("上线远程失败，查看消息监视器中的错误");
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
    /// 新增一个 VID 并落盘；对应入口为 AddVariable。
    /// 正文中的关键协作调用包括 Add、Datas、Add、Save、RefreshCatalog；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// shortcut: 新增后不重建 ReportID/CEID 关联，需要事件上报时在“事件与报表”页补 VID_Collection。
    public void AddVariable()
    {
        if (_disposed) return;
        var id = Model.NewVariableId?.Trim();
        var name = Model.NewVariableName?.Trim();
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
        {
            AddLog("提示", "请填写变量 ID 和名称");
            return;
        }
        var format = Model.NewVariableFormat;
        VIDBase table = Model.NewVariableCategoryIndex switch
        {
            1 => _catalog.DVID,
            2 => _catalog.ECID,
            _ => _catalog.SVID
        };
        // ID 必须跨表唯一（VIDManager.Load 会强制校验），所以这里也是全局查重
        if (_catalog.VIDDic_ID.ContainsKey(id))
        {
            AddLog("提示", $"变量 ID {id} 已存在（{_catalog.VIDDic_ID[id].QualifiedId}），VID 的 ID 需全局唯一");
            return;
        }
        if (_catalog.VIDDic_Name.ContainsKey(name))
        {
            AddLog("提示", $"变量名 {name} 已被 {_catalog.VIDDic_Name[name].QualifiedId} 占用（名字是全局的）");
            return;
        }

        var vid = new VIDInfo { ID = id, Name = name, Value = string.Empty, DataType = format, DataTypeName = format.ToString(), Category = table.Category };
        try
        {
            table.Datas.Add(vid);
            _catalog.VIDList.Add(vid);
            _catalog.VIDDic_Name[name] = vid;
            _catalog.VIDDic_ID[id] = vid;
            table.Save();
            RefreshCatalog();
            AddLog("变量", $"已新增 {table.Category}:{id}_{name} ({format})");
        }
        catch (Exception ex)
        {
            table.Datas.Remove(vid);
            _catalog.VIDList.Remove(vid);
            _catalog.VIDDic_Name.Remove(name);
            _catalog.VIDDic_ID.Remove(id);
            AddLog("错误", $"新增变量: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除选中的变量并落盘；对应入口为 DeleteVariable。
    /// <para>
    /// 被报表引用的变量不让删：<c>ReportID.Update</c> 遇到悬空引用直接抛，
    /// 整条 S6F11 组包失败。先删报表或把引用改掉，再回来删变量。
    /// </para>
    /// </summary>
    public void DeleteVariable()
    {
        if (_disposed) return;
        if (Model.SelectedVariable is not { } selected)
        {
            AddLog("提示", "请先选择要删除的变量");
            return;
        }
        var id = selected.Variable.ID;
        var users = _catalog.ReportID.Datas
            .Where(r => r.VID_Collection.Split(',').Select(VIDInfo.NormalizeId).Contains(id))
            .Select(r => r.ID)
            .ToArray();
        if (users.Length != 0)
        {
            AddLog("提示", $"变量 {id} 正被 Report {string.Join(",", users)} 引用，先删报表或改引用");
            return;
        }

        VIDBase table = selected.Category switch
        {
            "DVID" => _catalog.DVID,
            "ECID" => _catalog.ECID,
            _ => _catalog.SVID
        };
        try
        {
            foreach (var stale in table.Datas.Where(x => x.ID == id).ToArray()) table.Datas.Remove(stale);
            foreach (var stale in _catalog.VIDList.Where(x => x.ID == id && x.Category == table.Category).ToArray())
                _catalog.VIDList.Remove(stale);
            if (_catalog.VIDDic_Name.TryGetValue(selected.Variable.Name, out var named) && named.ID == id)
                _catalog.VIDDic_Name.Remove(selected.Variable.Name);
            _catalog.VIDDic_ID.Remove(id);
            table.Save();
            Model.SelectedVariable = null;
            RefreshCatalog();
            AddLog("变量", $"已删除 {table.Category}:{id}_{selected.Variable.Name}");
        }
        catch (Exception ex)
        {
            AddLog("错误", $"删除变量: {ex.Message}");
        }
    }

    /// <summary>
    /// 新增一个 Report 并落盘；对应入口为 AddReport。
    /// 正文中的关键协作调用包括 AddLog、SaveReports、RefreshCatalog；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// shortcut: 新增后不重建 CEID 侧引用，事件要挂这个 Report 得在同页再新增/改事件。
    public void AddReport()
    {
        if (_disposed) return;
        var id = Model.NewReportId?.Trim();
        var name = Model.NewReportName?.Trim();
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
        {
            AddLog("提示", "请填写 Report ID 和名称");
            return;
        }
        if (_catalog.ReportID.Datas.Any(x => x.ID == id))
        {
            AddLog("提示", $"Report {id} 已存在");
            return;
        }
        var vids = SplitIds(Model.NewReportVids);
        // ReportID.Update 遇到解析不出来的 VID 会直接抛，这里先拦一道。
        // 写 "SVID:102" 精确匹配；写裸 "102" 只在全表唯一时才认，否则提示补前缀。
        var unknown = vids.Where(v => !ResolveReportVid(v)).ToArray();
        if (unknown.Length != 0)
        {
            AddLog("提示", $"VID {string.Join(",", unknown)} 不存在");
            return;
        }
        try
        {
            _runtime.SaveReports(_catalog.ReportID.GetDatas()
                .Select(x => new SecsGemReportDefinition(x.ID, x.Name, x.VID_Collection))
                .Append(new SecsGemReportDefinition(id, name, string.Join(",", vids))));
            RefreshCatalog();
            AddLog("配置", $"已新增 Report {id}_{name}（VID {string.Join(",", vids)}）");
        }
        catch (Exception ex)
        {
            AddLog("错误", $"新增 Report: {ex.Message}");
        }
    }

    /// <summary>
    /// 新增一个 CEID 事件并落盘；对应入口为 AddEvent。
    /// 正文中的关键协作调用包括 AddLog、SaveEvents、RefreshCatalog；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    public void AddEvent()
    {
        if (_disposed) return;
        var id = Model.NewEventId?.Trim();
        var name = Model.NewEventName?.Trim();
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
        {
            AddLog("提示", "请填写 CEID 和事件名称");
            return;
        }
        if (_catalog.CEID.Datas.Any(x => x.ID == id))
        {
            AddLog("提示", $"事件 {id} 已存在");
            return;
        }
        var reports = SplitIds(Model.NewEventReports);
        // CEID.Update 遇到不存在的 ReportID 会直接抛。
        var unknown = reports.Where(r => _catalog.ReportID.Datas.All(x => x.ID != r)).ToArray();
        if (unknown.Length != 0)
        {
            AddLog("提示", $"Report {string.Join(",", unknown)} 不存在");
            return;
        }
        try
        {
            _runtime.SaveEvents(_catalog.CEID.GetDatas()
                .Select(x => new SecsGemEventDefinition(x.ID, x.Name, x.ReportID_Collection, x.Enable))
                .Append(new SecsGemEventDefinition(id, name, string.Join(",", reports), true)));
            RefreshCatalog();
            AddLog("配置", $"已新增事件 {id}_{name}（Report {string.Join(",", reports)}）");
        }
        catch (Exception ex)
        {
            AddLog("错误", $"新增事件: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除选中的 Report 并落盘；对应入口为 DeleteReport。
    /// <para>
    /// 被事件引用的报表不让删：<c>CEID.Update</c> 遇到悬空引用直接抛，同上。
    /// 落盘走 <c>SaveReports</c> 的 Merge（会删孤儿），和 <see cref="AddReport"/> 同一条路。
    /// </para>
    /// </summary>
    public void DeleteReport()
    {
        if (_disposed) return;
        if (Model.SelectedReport is not { } selected)
        {
            AddLog("提示", "请先选择要删除的报表");
            return;
        }
        var users = _catalog.CEID.Datas
            .Where(e => e.ReportID_Collection.Split(',').Select(x => x.Trim()).Contains(selected.ID))
            .Select(e => e.ID)
            .ToArray();
        if (users.Length != 0)
        {
            AddLog("提示", $"Report {selected.ID} 正被事件 {string.Join(",", users)} 引用，先删事件或改引用");
            return;
        }
        try
        {
            _runtime.SaveReports(_catalog.ReportID.GetDatas()
                .Where(x => x.ID != selected.ID)
                .Select(x => new SecsGemReportDefinition(x.ID, x.Name, x.VID_Collection)));
            Model.SelectedReport = null;
            RefreshCatalog();
            AddLog("配置", $"已删除 Report {selected.ID}_{selected.Name}");
        }
        catch (Exception ex)
        {
            AddLog("错误", $"删除 Report: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除选中的 CEID 事件并落盘；对应入口为 DeleteEvent。
    /// 事件是叶子（没有东西引用事件），直接删。落盘走 <c>SaveEvents</c>，和 <see cref="AddEvent"/> 同一条路。
    /// </summary>
    public void DeleteEvent()
    {
        if (_disposed) return;
        if (Model.SelectedEvent is not { } selected)
        {
            AddLog("提示", "请先选择要删除的事件");
            return;
        }
        try
        {
            _runtime.SaveEvents(_catalog.CEID.GetDatas()
                .Where(x => x.ID != selected.ID)
                .Select(x => new SecsGemEventDefinition(x.ID, x.Name, x.ReportID_Collection, x.Enable)));
            Model.SelectedEvent = null;
            RefreshCatalog();
            AddLog("配置", $"已删除事件 {selected.ID}_{selected.Name}");
        }
        catch (Exception ex)
        {
            AddLog("错误", $"删除事件: {ex.Message}");
        }
    }

    /// <summary>
    /// 报表里写的 VID 引用能否解析。裸 ID 即可；也认 "SVID:102" 这种带前缀的写法。
    /// 与 <c>ReportID.Update</c> 的判定保持一致，别让界面放行、运行时抛。
    /// </summary>
    private bool ResolveReportVid(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return false;
        var key = reference.Trim();
        var colon = key.IndexOf(':');
        if (colon >= 0) key = key[(colon + 1)..];
        return _catalog.VIDDic_ID.ContainsKey(key);
    }

    /// <summary>逗号分隔的 ID 列表；空串得到空列表，避免 ReportInfo.VID_Collection 留下空 ID。</summary>
    private static List<string> SplitIds(string csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? new List<string>()
            : csv.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

    /// <summary>
    /// 执行加载操作；对应入口为 LoadSample。
    /// <para>
    /// 以示例数据<b>覆盖</b>当前目录，而不是像以前那样拒绝：工作台启动时 App.OnInitialized 已经
    /// 自动 Config() 载入打底项，继续拒绝的话示例永远载不进去。
    /// 若当前表里是从磁盘 CSV 读出来的真实配置，覆盖会丢掉它 —— 这是"载入示例"的字面意思，日志里说明。
    /// </para>
    /// </summary>
    public void LoadSample()
    {
        if (_disposed) return;
        var replaced = _catalog.VIDList.Count != 0 || _catalog.CEID.Datas.Count != 0;
        _catalog.VIDList.Clear();
        _catalog.VIDDic_Name.Clear();
        _catalog.VIDDic_ID.Clear();
        _catalog.SVID.Datas.Clear();
        _catalog.DVID.Datas.Clear();
        _catalog.ECID.Datas.Clear();
        _catalog.CEID.Datas.Clear();
        _catalog.ReportID.Datas.Clear();
        _catalog.Alarm.Datas.Clear();

        var lot = new VIDInfo { ID = "1", Name = "LotId", Value = "LOT-42", DataType = SecsFormat.ASCII, Category = "SVID" };
        var clock = new VIDInfo { ID = "2", Name = "Clock", Value = "", DataType = SecsFormat.ASCII, Category = "SVID" };
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
        AddLog("配置", replaced
            ? "内存示例已载入（覆盖了原有目录，未落盘）；可编辑 LotId、上报 CEID 9 和 ALID 11"
            : "内存示例已载入；可编辑 LotId、上报 CEID 9 和 ALID 11");
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
            // 传输层在会话未建立时返回 null 而不是抛异常（见 SecsProtocolEngine.SendItemAsync 的同类修复），
            // 这里要求了等应答却没等到，必须报错，否则"发送成功"但 Host 什么都没收到。
            if (Model.WaitForReply && reply == null)
                throw new InvalidOperationException("未收到应答：会话未建立（HSMS 未 Selected）或连接已断开");
            if (reply != null)
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
            // 写完即落盘：原来值只在内存，重启就丢，这是全工作台唯一不持久化的编辑。
            // 文件小（几十行 CSV），单次写入成本可忽略，不值得为它单独放一个保存按钮。
            VIDBase table = selected.Category switch
            {
                "DVID" => _catalog.DVID,
                "ECID" => _catalog.ECID,
                _ => _catalog.SVID
            };
            table.Save();
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
