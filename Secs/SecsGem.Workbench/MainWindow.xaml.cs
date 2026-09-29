using Prism.Ioc;
using Secs.Variable;
using Secs.Manager;
using SecsGem.Core;
using SecsGem.Controls.Wpf.Terminal;
using SecsGem.Runtime;
using Secs4Net;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SecsGem.Workbench;

public partial class MainWindow : Window
{
    private readonly SecsGemRuntime _runtime;
    private readonly VIDManager _catalog;
    private readonly DispatcherTimer _stateTimer;

    public MainWindow(SecsGemRuntime runtime, IContainerProvider container)
    {
        _runtime = runtime;
        _catalog = container.Resolve<VIDManager>();
        InitializeComponent();

        _runtime.TerminalDisplay = new WpfTerminalDisplay();
        _runtime.SetEventHandler(new WorkbenchCommandHandler(this));
        _runtime.OnConnectChanged += (_, state) => Post(() =>
        {
            AddLog("HSMS", state.ToString());
            RefreshState();
        });
        _runtime.OnSend += (message, data) => LogMessageAsync("发送", message, data);
        _runtime.OnReceive += (message, data) => LogMessageAsync("接收", message, data);
        _runtime.OnReply += (message, data) => LogMessageAsync("应答", message, data);
        _runtime.OnInfo += message => Post(() => AddLog("信息", message));
        _runtime.OnWaring += message => Post(() => AddLog("警告", message));
        _runtime.OnError += message => Post(() => AddLog("错误", message));

        _stateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _stateTimer.Tick += (_, _) => RefreshState();
        _stateTimer.Start();
        RefreshState();
        RefreshCatalog();
    }

    private Task LogMessageAsync(string direction, SecsMessage message, string data)
    {
        Post(() => AddLog(direction, $"S{message.S}F{message.F} {data}"));
        return Task.CompletedTask;
    }

    private void Post(Action action)
    {
        if (!Dispatcher.HasShutdownStarted)
            Dispatcher.BeginInvoke(action);
    }

    private void AddLog(string kind, string message)
    {
        MessageLog.Items.Add($"{DateTime.Now:HH:mm:ss.fff} [{kind}] {message}");
        if (MessageLog.Items.Count > 1000)
            MessageLog.Items.RemoveAt(0);
        MessageLog.ScrollIntoView(MessageLog.Items[^1]);
    }

    private void RefreshState()
    {
        var state = _runtime.GetState();
        Overview.ConnectionStatus = state.IsConnected ? "HSMS Selected" : "未连接";
        Overview.ModeStatus = state.Mode.ToString();
        Overview.DeviceStatus = state.EquipmentStatus.ToString();
    }

    private void RefreshCatalog()
    {
        VariableGrid.ItemsSource = _catalog.SVID.GetDatas().Select(x => new VariableRow("SVID", x))
            .Concat(_catalog.DVID.GetDatas().Select(x => new VariableRow("DVID", x)))
            .Concat(_catalog.ECID.GetDatas().Select(x => new VariableRow("ECID", x)))
            .ToList();
        EventGrid.ItemsSource = _catalog.CEID.GetDatas();
        ReportGrid.ItemsSource = _catalog.ReportID.GetDatas();
        AlarmGrid.ItemsSource = _catalog.Alarm.GetDatas();
    }

    private async Task RunAsync(string operation, Func<Task> action)
    {
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

    private async void Connect_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("连接", async () =>
        {
            var options = new SecsGem.Core.SecsGemOptions
            {
                IsActiveMode = ConnectionMode.SelectedIndex == 0,
                IpAddress = AddressBox.Text.Trim(),
                Port = int.Parse(PortBox.Text),
                DeviceId = int.Parse(DeviceIdBox.Text)
            };
            if (!await _runtime.ConnectAsync(options))
                throw new InvalidOperationException("连接未建立；请检查对端与端口");
        });

    private async void Disconnect_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("断开", _runtime.DisconnectAsync);

    private async void Establish_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("S1F13", _runtime.EstablishCommunicationAsync);

    private async void Online_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("Online Remote", async () =>
        {
            if (!await _runtime.GoOnlineRemoteAsync())
                throw new InvalidOperationException("上线失败，查看消息监视器中的错误");
        });

    private async void Offline_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("Offline", async () =>
        {
            if (!await _runtime.GoOfflineAsync())
                throw new InvalidOperationException("下线失败，查看消息监视器中的错误");
        });

    private void LoadConfig_Click(object sender, RoutedEventArgs e)
    {
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

    private void LoadSample_Click(object sender, RoutedEventArgs e)
    {
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

    private async void SendSml_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("发送 SML", async () =>
        {
            if (!_runtime.IsConnected)
                throw new InvalidOperationException("请先建立 HSMS 连接");
            if (string.IsNullOrWhiteSpace(SmlBox.Text))
                throw new ArgumentException("请输入 SML 消息");
            var reply = await _runtime.Send(SmlBox.Text, WaitReplyBox.IsChecked == true);
            if (WaitReplyBox.IsChecked == true && reply != null)
                AddLog("手工应答", $"S{reply.S}F{reply.F} {reply.SecsItem}");
        });

    private void SetVariable_Click(object sender, RoutedEventArgs e)
    {
        if (VariableGrid.SelectedItem is not VariableRow selected)
        {
            AddLog("提示", "请先选择变量");
            return;
        }
        try
        {
            _runtime.SetVariable(selected.Name, VariableValueBox.Text);
            RefreshCatalog();
            AddLog("变量", $"{selected.Name} = {VariableValueBox.Text}");
        }
        catch (Exception ex)
        {
            AddLog("错误", $"设置变量: {ex.Message}");
        }
    }

    private void RefreshCatalog_Click(object sender, RoutedEventArgs e) => RefreshCatalog();

    private async void ReportEvent_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("S6F11", async () =>
        {
            if (EventGrid.SelectedItem is not CEIDInfo selected)
                throw new InvalidOperationException("请先选择 CEID");
            if (!await _runtime.ReportEventAsync(selected.Name))
                throw new InvalidOperationException("事件上报失败，查看消息监视器中的错误");
        });

    private async void RaiseAlarm_Click(object sender, RoutedEventArgs e) => await SendAlarmAsync(1);
    private async void ResetAlarm_Click(object sender, RoutedEventArgs e) => await SendAlarmAsync(0);

    private async Task SendAlarmAsync(byte code) => await RunAsync("S5F1", async () =>
    {
        if (AlarmGrid.SelectedItem is not AlarmInfo selected)
            throw new InvalidOperationException("请先选择报警");
        if (!await _runtime.ReportAlarmAsync(new SecsGemAlarm(selected.ID, selected.Description, code, selected.Enable)))
            throw new InvalidOperationException("报警上报失败，查看消息监视器中的错误");
    });

    private void PreviewTerminal_Click(object sender, RoutedEventArgs e) =>
        _runtime.TerminalDisplay.Show("S10 预览", "Host 终端消息显示预览");

    private void ClearLog_Click(object sender, RoutedEventArgs e) => MessageLog.Items.Clear();

    protected override void OnClosed(EventArgs e)
    {
        _stateTimer.Stop();
        _runtime.Dispose();
        base.OnClosed(e);
    }

    private sealed record VariableRow(string Category, VIDInfo Variable)
    {
        public string Id => Variable.ID;
        public string Name => Variable.Name;
        public string Format => Variable.DataType.ToString();
        public string Value => Variable.Value?.ToString() ?? string.Empty;
    }

    private sealed class WorkbenchCommandHandler(MainWindow window) : ISecsGemEventHandler
    {
        public Task<byte> OnRemoteCommandAsync(SecsGemRemoteCommand command)
        {
            return window.Dispatcher.InvokeAsync(() =>
            {
                var parameters = string.Join(", ", command.Parameters.Select(x => $"{x.Key}={x.Value}"));
                window.AddLog("S2F41", $"{command.Name} {parameters}");
                return byte.TryParse(window.HcackBox.Text, out var hcack) ? hcack : (byte)0;
            }).Task;
        }
    }
}
