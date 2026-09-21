using Device.Core.Devices;
using Device.Core.Lights;

namespace Device.Drivers.Lights;

/// <summary>
/// 串口光源控制器的共享生命周期和参数检查实现。
/// </summary>
internal abstract class SerialLightControllerBase : ILightController
{
    private readonly object _operationGate = new();

    /// <summary>使用不可变配置创建控制器。</summary>
    /// <param name="options">已经由工厂验证的配置。</param>
    protected SerialLightControllerBase(LightControllerOptions options)
    {
        Options = options;
    }

    /// <inheritdoc />
    public string Name => Options.Name;

    /// <inheritdoc />
    public DeviceState State { get; private set; } = DeviceState.Disconnected;

    /// <inheritdoc />
    public LightControllerOptions Options { get; }

    /// <summary>普通失败使用的稳定错误码前缀。</summary>
    protected abstract string ErrorCodePrefix { get; }

    /// <summary>厂商函数表示成功的返回值。</summary>
    protected abstract int SuccessCode { get; }

    /// <summary>建立厂商连接。</summary>
    protected abstract int ConnectCore();

    /// <summary>释放厂商连接。</summary>
    protected abstract int DisconnectCore();

    /// <summary>调用厂商函数设置单通道亮度。</summary>
    protected abstract int SetIntensityCore(int channel, int intensity);

    /// <inheritdoc />
    public ValueTask<DeviceResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_operationGate)
        {
            if (State == DeviceState.Connected)
                return ValueTask.FromResult(DeviceResult.Ok());

            State = DeviceState.Connecting;
            try
            {
                var result = ConnectCore();
                if (result == SuccessCode)
                {
                    State = DeviceState.Connected;
                    return ValueTask.FromResult(DeviceResult.Ok());
                }

                State = DeviceState.Faulted;
                return ValueTask.FromResult(DeviceResult.Failure(
                    $"{ErrorCodePrefix}.connect_failed",
                    $"光源控制器连接失败，厂商返回码：{result}。"));
            }
            catch (Exception exception)
            {
                State = DeviceState.Faulted;
                return ValueTask.FromResult(DeviceResult.Failure(
                    $"{ErrorCodePrefix}.connect_failed",
                    exception.Message));
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<DeviceResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_operationGate)
        {
            if (State == DeviceState.Disconnected)
                return ValueTask.FromResult(DeviceResult.Ok());

            try
            {
                var result = DisconnectCore();
                if (result == SuccessCode)
                {
                    State = DeviceState.Disconnected;
                    return ValueTask.FromResult(DeviceResult.Ok());
                }

                State = DeviceState.Faulted;
                return ValueTask.FromResult(DeviceResult.Failure(
                    $"{ErrorCodePrefix}.disconnect_failed",
                    $"光源控制器断开失败，厂商返回码：{result}。"));
            }
            catch (Exception exception)
            {
                State = DeviceState.Faulted;
                return ValueTask.FromResult(DeviceResult.Failure(
                    $"{ErrorCodePrefix}.disconnect_failed",
                    exception.Message));
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<DeviceResult> SetIntensityAsync(
        int channel,
        int intensity,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_operationGate)
            return ValueTask.FromResult(SetIntensityLocked(channel, intensity));
    }

    /// <inheritdoc />
    public ValueTask<DeviceResult> SetAllIntensityAsync(
        int intensity,
        CancellationToken cancellationToken = default)
    {
        lock (_operationGate)
        {
            var validation = ValidateOperation(1, intensity);
            if (validation is not null)
                return ValueTask.FromResult(validation);

            for (var channel = 1; channel <= Options.ChannelCount; channel++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int result;
                try
                {
                    result = SetIntensityCore(channel, intensity);
                }
                catch (Exception exception)
                {
                    // 与单通道设置保持一致：厂商 DLL 异常转换为稳定结果，不泄漏原生调用细节。
                    State = DeviceState.Faulted;
                    return ValueTask.FromResult(DeviceResult.Failure(
                        $"{ErrorCodePrefix}.set_intensity_failed",
                        $"设置通道 {channel} 亮度失败：{exception.Message}"));
                }

                if (result != SuccessCode)
                {
                    return ValueTask.FromResult(DeviceResult.Failure(
                        $"{ErrorCodePrefix}.set_intensity_failed",
                        $"设置通道 {channel} 亮度失败，厂商返回码：{result}。"));
                }
            }

            return ValueTask.FromResult(DeviceResult.Ok());
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    private DeviceResult SetIntensityLocked(int channel, int intensity)
    {
        var validation = ValidateOperation(channel, intensity);
        if (validation is not null)
            return validation;

        try
        {
            var result = SetIntensityCore(channel, intensity);
            return result == SuccessCode
                ? DeviceResult.Ok()
                : DeviceResult.Failure(
                    $"{ErrorCodePrefix}.set_intensity_failed",
                    $"设置通道 {channel} 亮度失败，厂商返回码：{result}。");
        }
        catch (Exception exception)
        {
            State = DeviceState.Faulted;
            return DeviceResult.Failure($"{ErrorCodePrefix}.set_intensity_failed", exception.Message);
        }
    }

    private DeviceResult? ValidateOperation(int channel, int intensity)
    {
        if (State != DeviceState.Connected)
        {
            return DeviceResult.Failure(
                $"{ErrorCodePrefix}.not_connected",
                $"光源控制器 '{Name}' 尚未连接。");
        }

        if (channel < 1 || channel > Options.ChannelCount)
        {
            return DeviceResult.Failure(
                $"{ErrorCodePrefix}.channel_out_of_range",
                $"通道必须在 1 到 {Options.ChannelCount} 之间。");
        }

        if (intensity < 0 || intensity > Options.MaximumIntensity)
        {
            return DeviceResult.Failure(
                $"{ErrorCodePrefix}.intensity_out_of_range",
                $"亮度必须在 0 到 {Options.MaximumIntensity} 之间。");
        }

        return null;
    }
}
