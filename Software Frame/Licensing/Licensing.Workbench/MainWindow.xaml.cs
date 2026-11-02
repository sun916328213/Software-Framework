using System.Windows;
using System.Windows.Threading;
using Licensing.Native.Client;
using Licensing.Trust;
using Licensing.Wpf;

namespace Licensing.Workbench;

public partial class MainWindow : Window
{
    private readonly string? _key, _product;
    private readonly TimeSpan _interval;
    private readonly string _identitySource = string.Empty;
    private readonly CancellationTokenSource _lifetime = new();
    private WpfLicenseSession? _session;
    private bool _running, _authorized, _closing;
    internal int StartedTasks { get; private set; }
    internal int CompletedTasks { get; private set; }
    internal bool TaskInProgress => _running;
    internal Task? MonitorCompletion => _session?.Completion;

    public MainWindow() : this(null, null, readEmbedded: true) { }

    internal MainWindow(string? publicKey, string? productId, TimeSpan? interval = null, bool readEmbedded = false)
    {
        InitializeComponent();
        _interval = interval ?? TimeSpan.FromSeconds(1);
        try
        {
            if (readEmbedded)
            {
                // Workbench 只是体量小的生产宿主：信任锚和生产程序、激活工具走同一条编译期通道，
                // 不在运行时读任何外部配置，否则三个宿主的验证语义会分叉。
                publicKey = EmbeddedPublicKey();
                productId = EmbeddedProductId();
                _identitySource = "编译期嵌入（与激活工具、生产程序同一来源）";
            }
            Machine.Text = NativeLicenseSession.GetMachineCode();
        }
        catch (Exception ex) { Append("组件初始化失败：" + ex.Message); }
        _key = publicKey; _product = productId;
        Loaded += OnLoaded;
        Closed += OwnerClosed;
    }

    private static string? EmbeddedPublicKey() => TrustedIssuer.PublicKey;

    private static string? EmbeddedProductId() => TrustedIssuer.ProductId;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (string.IsNullOrWhiteSpace(_key) || string.IsNullOrWhiteSpace(_product))
        {
            Authorization.Text = "未配置发行身份：编译期没有嵌入公钥或产品标识。";
            return;
        }
        if (_identitySource.Length > 0) Append("身份来源：" + _identitySource + "，产品：" + _product);
        try
        {
            _session = WpfLicenseSession.Start(this, _key, _product, async (result, token) =>
                await Dispatcher.InvokeAsync(() => Apply(result), DispatcherPriority.Normal, token).Task,
                interval: _interval);
            _ = ObserveCompletionAsync(_session);
        }
        catch (Exception ex) { Deny("监控初始化失败：" + ex.Message); }
    }

    private void Apply(NativeVerifyResult result)
    {
        if (_closing) return;
        _authorized = result.Ok;
        StartTask.IsEnabled = _authorized && !_running;
        Authorization.Text = result.Message + (result.License == null ? "" : $"\n产品：{result.License.ProductId}　客户：{result.License.Customer}");
        Append(result.Ok ? "授权有效，可申请新任务。" : "禁止新任务：" + result.Message);
        // 不取消已获准的模拟任务；真实设备安全策略属于未来的 MachineApp。
    }

    private void Deny(string message)
    {
        _authorized = false; StartTask.IsEnabled = false; Authorization.Text = message; Append(message);
    }

    private async Task ObserveCompletionAsync(WpfLicenseSession session)
    {
        string message;
        try { await session.Completion.ConfigureAwait(false); message = "授权监控已停止，禁止新任务。"; }
        catch (Exception ex) { message = "授权监控异常，禁止新任务：" + ex.Message; }
        if (_closing || Dispatcher.HasShutdownStarted) return;
        try { await Dispatcher.InvokeAsync(() => { if (!_closing) Deny(message); }); }
        catch (TaskCanceledException) when (_closing || Dispatcher.HasShutdownStarted) { }
    }

    private async void Start_Click(object sender, RoutedEventArgs e) => await RunSimulationAsync();

    internal async Task<bool> RunSimulationAsync(TimeSpan? duration = null)
    {
        if (_running || _closing || _session == null) return false;
        _running = true; StartTask.IsEnabled = false;
        CancellationToken token = _lifetime.Token;
        try
        {
            var check = await _session.CheckNowAsync(token);
            token.ThrowIfCancellationRequested();
            Apply(check.Verification);
            if (!check.Verification.Ok) return false;
            StartedTasks++;
            Append("模拟任务已开始；授权到期不会中断它。");
            await Task.Delay(duration ?? TimeSpan.FromSeconds(10), token);
            CompletedTasks++;
            Append("模拟任务自然完成。");
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { Append("窗口关闭，模拟任务已取消。"); return false; }
        catch (Exception ex) { Deny("任务入口检查失败：" + ex.Message); return false; }
        finally
        {
            _running = false;
            StartTask.IsEnabled = !_closing && _authorized && _session?.Completion.IsCompleted == false;
        }
    }

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        if (_closing || _session == null) return;
        try { Apply((await _session.CheckNowAsync(_lifetime.Token)).Verification); }
        catch (OperationCanceledException) when (_closing) { }
        catch (Exception ex) { Deny("即时检查失败：" + ex.Message); }
    }

    private void Append(string text) { Log.AppendText($"{DateTime.Now:HH:mm:ss} {text}{Environment.NewLine}"); Log.ScrollToEnd(); }
    private void OwnerClosed(object? sender, EventArgs e)
    {
        _closing = true; _authorized = false; StartTask.IsEnabled = false;
        _lifetime.Cancel(); _lifetime.Dispose();
        Loaded -= OnLoaded; Closed -= OwnerClosed;
        // WpfLicenseSession 自己在 owner.Closed 中取消并异步释放，不从回调中等待它自身。
    }
}
