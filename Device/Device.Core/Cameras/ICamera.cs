using Device.Core.Devices;

namespace Device.Core.Cameras;

/// <summary>
/// 应用层使用的相机接口，不暴露任何厂商 SDK 类型。
/// </summary>
public interface ICamera : IDevice
{
    /// <summary>创建该相机时使用的配置快照。</summary>
    CameraOptions Options { get; }

    /// <summary>
    /// 等待并返回一帧图像。帧触发源为 Software 时先发送软件触发命令；
    /// 自由运行或外部触发时等待相机返回下一帧。
    /// </summary>
    /// <param name="cancellationToken">用于在触发或等待图像期间取消采集。</param>
    /// <returns>采集到的图像帧。</returns>
    ValueTask<ImageFrame> CaptureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 在已连接的相机上直接下发曝光和增益，不需要断开重连。
    /// </summary>
    /// <param name="exposureMilliseconds">曝光时间，单位为毫秒，必须大于 0。</param>
    /// <param name="gain">相机增益，不能小于 0。</param>
    /// <param name="cancellationToken">用于在开始下发前或驱动支持时取消操作。</param>
    /// <returns>包含下发是否成功以及失败原因的结果。</returns>
    ValueTask<DeviceResult> SetExposureGainAsync(
        int exposureMilliseconds,
        double gain,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在已连接的相机上直接下发触发配置，不需要断开重连。
    /// </summary>
    /// <param name="frameTrigger">帧触发配置；面阵与线扫相机都会应用。</param>
    /// <param name="lineTrigger">行触发配置；仅线扫相机应用，面阵相机忽略。</param>
    /// <param name="cancellationToken">用于在开始下发前或驱动支持时取消操作。</param>
    /// <returns>包含下发是否成功以及失败原因的结果。</returns>
    /// <remarks>
    /// 下发成功后，后续 <see cref="CaptureAsync"/> 会按新的帧触发配置决定是否主动发送软件触发命令。
    /// 配置非法（例如启用触发但没有触发源）时返回失败，不会改动相机上的当前设置。
    /// </remarks>
    ValueTask<DeviceResult> SetTriggerAsync(
        CameraTriggerSettings frameTrigger,
        CameraTriggerSettings lineTrigger,
        CancellationToken cancellationToken = default);
}
