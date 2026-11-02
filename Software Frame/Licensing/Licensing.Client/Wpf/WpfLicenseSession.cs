using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Licensing.Native.Client;

namespace Licensing.Wpf;

/// <summary>
/// WPF 宿主适配。Start/DisposeAsync 必须在窗口 Dispatcher 上调用，Start 要求窗口已 Loaded。
/// 宿主必须默认禁止新任务，任务入口 await CheckNowAsync 后按返回值放行，并处理失效回调和 Completion。
/// 本组件不直接控制设备、禁用窗口或退出进程。
/// </summary>
public sealed class WpfLicenseSession : IAsyncDisposable
{
    private static readonly DependencyProperty SessionProperty = DependencyProperty.RegisterAttached(
        "Session", typeof(WpfLicenseSession), typeof(WpfLicenseSession), new PropertyMetadata(null));
    private readonly Window _owner;
    private readonly Func<NativeVerifyResult, CancellationToken, Task> _onStateChanged;
    private readonly NativeLicenseMonitor _monitor;
    private NativeLicenseStatus? _previousStatus;
    private string? _previousLicenseId;
    private LicenseNotificationWindow? _notification;
    private bool _notificationIsError;
    private bool _stopping;
    private Task? _disposeTask;

    private WpfLicenseSession(Window owner, string trustedPublicKey, string productId,
        Func<NativeVerifyResult, CancellationToken, Task> onStateChanged, string? requiredFeature, TimeSpan? interval)
    {
        _owner = owner;
        _onStateChanged = onStateChanged;
        _monitor = NativeLicenseMonitor.Start(trustedPublicKey, productId, OnCheckedAsync, interval: interval, requiredFeature: requiredFeature);
        owner.Closed += OwnerClosed;
    }

    public static WpfLicenseSession Start(Window owner, string trustedPublicKey, string productId,
        Func<NativeVerifyResult, CancellationToken, Task> onStateChanged, string? requiredFeature = null, TimeSpan? interval = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(onStateChanged);
        owner.Dispatcher.VerifyAccess();
        if (!owner.IsLoaded || !owner.IsVisible)
            throw new InvalidOperationException("请在主窗口 Loaded 后启动授权会话，设备任务应保持禁用直到首次检查通过。");
        if (owner.GetValue(SessionProperty) != null)
            throw new InvalidOperationException("该窗口已有授权会话，请勿重复初始化。");
        var session = new WpfLicenseSession(owner, trustedPublicKey, productId, onStateChanged, requiredFeature, interval);
        owner.SetValue(SessionProperty, session);
        return session;
    }

    public NativeCheckResult Current => _monitor.Current;

    /// <summary>新任务入口应 await 此方法并检查返回值，不以缓存状态或按钮状态代替即时检查。</summary>
    public Task<NativeCheckResult> CheckNowAsync(CancellationToken cancellationToken = default) =>
        _monitor.CheckNowAsync(cancellationToken);

    public Task Completion => _monitor.Completion;
    public Exception? ShutdownError { get; private set; }

    private async Task OnCheckedAsync(NativeCheckResult result, CancellationToken token)
    {
        bool changed = result.Verification.Status != _previousStatus ||
                       result.Verification.License?.Id != _previousLicenseId;
        if (changed)
        {
            // 回调在后台执行，宿主自行切换 UI 线程；不要在回调内等待释放本会话。
            await _onStateChanged(result.Verification, token).ConfigureAwait(false);
            _previousStatus = result.Verification.Status;
            _previousLicenseId = result.Verification.License?.Id;
        }
        await _owner.Dispatcher.InvokeAsync(() => UpdateNotification(result, changed),
            DispatcherPriority.Normal, token).Task.ConfigureAwait(false);
    }

    private void UpdateNotification(NativeCheckResult result, bool changed)
    {
        if (_stopping || !_owner.IsVisible) return;
        if (result.Verification.Ok)
        {
            if (_notificationIsError) CloseNotification();
            if (!result.ShouldRemind) return;
            string expiry = result.Verification.License!.ExpireAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            ShowNotification($"软件使用期限即将到期（{expiry}），请联系供应商办理续期。", false);
        }
        else if (changed)
        {
            string message = result.Verification.Status == NativeLicenseStatus.Expired
                ? "软件使用期限已到期，请联系供应商办理续期。"
                : result.Verification.Message;
            ShowNotification(message, true);
        }
    }

    private void ShowNotification(string message, bool isError)
    {
        _notificationIsError = isError;
        if (_notification == null)
        {
            var window = new LicenseNotificationWindow(_owner);
            window.Closed += (_, _) => { if (ReferenceEquals(_notification, window)) _notification = null; };
            _notification = window;
            window.SetMessage(message);
            window.Show();
        }
        else _notification.SetMessage(message);
    }

    private void CloseNotification()
    {
        _notification?.Close();
        _notification = null;
        _notificationIsError = false;
    }

    private async void OwnerClosed(object? sender, EventArgs e)
    {
        try { await DisposeAsync(); }
        catch (Exception ex)
        {
            ShutdownError = ex;
            Trace.TraceError($"授权会话退出失败：{ex}");
        }
    }

    public ValueTask DisposeAsync()
    {
        _owner.Dispatcher.VerifyAccess();
        _stopping = true;
        return new ValueTask(_disposeTask ??= StopAsync());
    }

    private async Task StopAsync()
    {
        try { await _monitor.DisposeAsync(); }
        finally
        {
            _owner.Closed -= OwnerClosed;
            if (ReferenceEquals(_owner.GetValue(SessionProperty), this)) _owner.ClearValue(SessionProperty);
            CloseNotification();
        }
    }
}
