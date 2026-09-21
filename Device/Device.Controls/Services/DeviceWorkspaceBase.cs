using Device.Core.Devices;
using Device.Controls.Models;
using Extension.Core.Conversion;
using Extension.UI.Controls;

namespace Device.Controls.Services;

/// <summary>
/// 设备测试页流程基类（对标 CameraWorkspace 的通用部分）：
/// 驱动目录填充、档案读盘/兜底、按指纹创建设备实例、连接/断开、ConfigManagerView 回调。
/// 只依赖 Device.Core 抽象与工厂契约，不引用 Device.Drivers。
/// </summary>
public abstract class DeviceWorkspaceBase<TModel, TProfile>
    where TModel : DevicePageModelBase<TProfile>, new()
    where TProfile : class, IDeviceProfile, new()
{
    private string? _currentKey;
    private IDevice? _device;

    /// <summary>页状态（单例 Workspace 持有，VM 直接暴露）。</summary>
    public TModel Model { get; } = new();

    protected DeviceWorkspaceBase(IReadOnlyList<DeviceDriverDescriptor> availableDrivers)
    {
        foreach (var descriptor in availableDrivers)
            Model.Drivers.Add(new DeviceDriverOption(descriptor.Id.Value, descriptor.DisplayName));

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
    protected string DefaultDriverId => Model.Drivers[0].Value;

    protected IDevice? Device => _device;

    public async Task ConnectAsync()
    {
        var device = await EnsureDeviceAsync(Model.SelectedProfileOrThrow()).ConfigureAwait(false);
        var result = await device.ConnectAsync().ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "连接失败");

        DispatcherEx.UIInvokeSafe(() =>
        {
            Model.IsConnected = true;
            Model.StatusText = "已连接";
            OnConnectionChanged(true);
        });
    }

    public async Task DisconnectAsync()
    {
        if (_device is null)
            return;

        var result = await _device.DisconnectAsync().ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "断开失败");

        DispatcherEx.UIInvokeSafe(() =>
        {
            Model.IsConnected = false;
            Model.StatusText = "未连接";
            OnConnectionChanged(false);
        });
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

    protected TProfile NewProfile(string? name = null) => new()
    {
        Name = name ?? $"{CategoryName}{Model.DataCollection.Count + 1}",
        DriverId = DefaultDriverId,
    };

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
        catch
        {
            // 重建路径上的释放失败不阻断新实例创建。
        }
    }

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
