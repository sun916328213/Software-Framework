// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：DeviceWorkspaceBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、Device.Core.Devices、Device.Controls.Models、Extension.Core.Conversion、Extension.UI.Controls。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using Device.Core.Devices;
using Device.Controls.Models;
using Extension.Core.Conversion;
using Extension.UI.Controls;

namespace Device.Controls.Services;

/// <summary>
/// 设备测试页流程基类（对标 CameraWorkspace 的通用部分）：
/// 驱动目录填充、档案读盘/兜底、按指纹创建设备实例、连接/断开、ConfigManagerView 回调。
/// 只依赖 Device.Core 抽象与工厂契约，不引用 Device.Runtime。
/// </summary>
public abstract class DeviceWorkspaceBase<TModel, TProfile> : IAsyncDisposable
    where TModel : DevicePageModelBase<TProfile>, new()
    where TProfile : class, IDeviceProfile, new()
{
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _disposeTask;
    public bool IsClosed { get; private set; }
    private string? _currentKey;
    private IDevice? _device;

    /// <summary>页状态（单例 Workspace 持有，VM 直接暴露）。</summary>
    public TModel Model { get; } = new();

    /// <summary>
    /// 初始化 DeviceWorkspaceBase 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 Add、LoadProfiles；具体条件和先后次序见方法体。
    /// </summary>
    protected DeviceWorkspaceBase(IReadOnlyList<DeviceDriverDescriptor> availableDrivers)
    {
        foreach (var descriptor in availableDrivers)
            Model.Drivers.Add(descriptor.Id.Value);

        if (Model.Drivers.Count == 0)
            throw new InvalidOperationException($"未注册任何{CategoryName}驱动，无法初始化配置。");

        LoadProfiles();
    }

    /// <summary>类别中文名（日志与错误文案用）。</summary>
    protected abstract string CategoryName { get; }

    /// <summary>无档案时的兜底档案名。</summary>
    protected abstract string FallbackProfileName { get; }

    /// <summary>档案指纹：连接要素任一改动都重建实例。</summary>
    protected abstract string BuildKey(TProfile profile);

    /// <summary>通过统一工厂创建尚未连接的设备实例。</summary>
    protected abstract IDevice CreateDevice(TProfile profile);

    /// <summary>工厂目录中的第一个驱动作为新档案和旧空配置的默认值。</summary>
    protected string DefaultDriverId => Model.Drivers[0];

    protected IDevice? Device => _device;

    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ThrowIf、ConfigureAwait、WaitAsync、EnsureDeviceAsync、SelectedProfileOrThrow；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public virtual async Task ConnectAsync()
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        await _lifecycleGate.WaitAsync(_lifetime.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(IsClosed, this);
            var device = await EnsureDeviceAsync(Model.SelectedProfileOrThrow()).ConfigureAwait(false);
            var result = await device.ConnectAsync(_lifetime.Token).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "连接失败");

            DispatcherEx.UIInvokeSafe(() =>
            {
                if (IsClosed) return;
                Model.IsConnected = true;
                Model.StatusText = "已连接";
                OnConnectionChanged(true);
            });
        }
        finally { _lifecycleGate.Release(); }
    }

    /// <summary>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 ThrowIf、ConfigureAwait、WaitAsync、DisconnectAsync、UIInvokeSafe；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public virtual async Task DisconnectAsync()
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        await _lifecycleGate.WaitAsync(_lifetime.Token).ConfigureAwait(false);
        try
        {
            if (_device is null) return;
            var result = await _device.DisconnectAsync(_lifetime.Token).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "断开失败");

            DispatcherEx.UIInvokeSafe(() =>
            {
                if (IsClosed) return;
                Model.IsConnected = false;
                Model.StatusText = "未连接";
                OnConnectionChanged(false);
            });
        }
        finally { _lifecycleGate.Release(); }
    }

    /// <summary>选中行切换钩子（ConfigManagerView SelectedCallback）。</summary>
    public virtual void SetCurDevice()
    {
    }

    /// <summary>档案集合落盘（Config/&lt;类别&gt;.json）。</summary>
    public void SaveProfiles() =>
        DispatcherEx.UIInvokeSafe(() =>
            Model.DataCollection.Save(Model.FilePath!, Model.FileName!));

    /// <summary>ConfigManagerView 增加按钮回调：控件已加空行，此处命名 + 选中。</summary>
    public void CompleteAddedProfile()
    {
        var profile = Model.DataCollection.FirstOrDefault(p => string.IsNullOrWhiteSpace(p.Name));
        if (profile is null)
        {
            profile = NewProfile();
            Model.DataCollection.Add(profile);
        }

        profile.Name = $"{CategoryName}{Model.DataCollection.Count}";
        if (string.IsNullOrWhiteSpace(profile.DriverId))
            profile.DriverId = DefaultDriverId;
        Model.SelectedProfile = profile;
    }

    /// <summary>ConfigManagerView 删除按钮回调：保底至少留一个。</summary>
    public void FixAfterDelete()
    {
        if (Model.DataCollection.Count == 0)
        {
            var fallback = NewProfile(FallbackProfileName);
            Model.DataCollection.Add(fallback);
            Model.SelectedProfile = fallback;
        }
        else if (Model.SelectedProfile is null || !Model.DataCollection.Contains(Model.SelectedProfile))
        {
            Model.SelectedProfile = Model.DataCollection[^1];
        }
    }

    /// <summary>按档案确保设备实例（指纹命中复用，否则释放旧实例重建；不连接硬件）。</summary>
    protected async ValueTask<IDevice> EnsureDeviceAsync(TProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var key = BuildKey(profile);
        if (_device is not null && _currentKey == key)
            return _device;

        await ReleaseAsync().ConfigureAwait(false);
        _device = CreateDevice(profile);
        _currentKey = key;
        OnDeviceCreated(_device);
        return _device;
    }

    /// <summary>取已连接设备，未连接即抛“请先连接”。</summary>
    protected T Connected<T>()
        where T : class, IDevice
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        if (_device is not T device || device.State != DeviceState.Connected)
            throw new InvalidOperationException($"请先连接{CategoryName}");
        return device;
    }

    /// <summary>实例创建后钩子（订阅事件用）。</summary>
    protected virtual void OnDeviceCreated(IDevice device)
    {
    }

    /// <summary>实例即将释放（退订事件用；参数为待释放实例）。</summary>
    protected virtual void OnDeviceReleasing(IDevice device)
    {
    }

    /// <summary>连接状态变化（UI 线程）。</summary>
    protected virtual void OnConnectionChanged(bool connected)
    {
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 NewProfile。
    /// </summary>
    protected TProfile NewProfile(string? name = null) => new()
    {
        Name = name ?? $"{CategoryName}{Model.DataCollection.Count + 1}",
        DriverId = DefaultDriverId,
    };

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReleaseAsync。
    /// 正文中的关键协作调用包括 OnDeviceReleasing、ConfigureAwait、DisposeAsync、TraceError；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async ValueTask ReleaseAsync()
    {
        if (_device is null)
            return;

        var old = _device;
        _device = null;
        _currentKey = null;
        OnDeviceReleasing(old);

        try
        {
            await old.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceError("Device release failed: {0}", error);
            // 重建路径上的释放失败不阻断新实例创建。
        }
    }

    /// <summary>Cancel connection work, detach device events and release the owned device once.</summary>
    /// <remarks>
    /// 请求取消当前操作；对应入口为 CancelPendingOperations。
    /// 正文中的关键协作调用包括 Cancel；具体条件和先后次序见方法体。
    /// </remarks>
    public void CancelPendingOperations() { if (!IsClosed) _lifetime.Cancel(); }

    /// <summary>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 Cancel、DisposeCoreAsync；具体条件和先后次序见方法体。
    /// </summary>
    public virtual ValueTask DisposeAsync()
    {
        if (_disposeTask is not null) return new(_disposeTask);
        IsClosed = true;
        _lifetime.Cancel();
        _disposeTask = DisposeCoreAsync();
        return new(_disposeTask);
    }

    /// <summary>
    /// 执行资源释放步骤；对应入口为 DisposeCoreAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ReleaseAsync、Release、Dispose；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    private async Task DisposeCoreAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try { await ReleaseAsync().ConfigureAwait(false); }
        finally { _lifecycleGate.Release(); _lifetime.Dispose(); }
    }

    /// <summary>
    /// 执行加载操作；对应入口为 LoadProfiles。
    /// 正文中的关键协作调用包括 TryRead、Clear、Where、IsNullOrWhiteSpace、Add；具体条件和先后次序见方法体。
    /// </summary>
    private void LoadProfiles()
    {
        var profiles = JsonEx.TryRead<List<TProfile>>(Model.FullPath!);
        if (profiles is { Count: > 0 })
        {
            Model.DataCollection.Clear();
            foreach (var profile in profiles.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
            {
                if (string.IsNullOrWhiteSpace(profile.DriverId))
                    profile.DriverId = DefaultDriverId;
                Model.DataCollection.Add(profile);
            }
            Model.SelectedProfile = Model.DataCollection.FirstOrDefault();
        }

        if (Model.DataCollection.Count == 0)
        {
            var fallback = NewProfile(FallbackProfileName);
            Model.DataCollection.Add(fallback);
            Model.SelectedProfile = fallback;
        }
    }
}
