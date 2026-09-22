using System.Net;

namespace Device.Core.Cameras;

/// <summary>
/// 集中维护相机配置不变量，避免每个驱动重复实现并产生不同规则。
/// </summary>
public static class CameraOptionsValidator
{
    /// <summary>
    /// 验证相机配置的全部公共不变量。
    /// </summary>
    /// <param name="options">需要验证的相机配置。</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="CameraConfigurationException">配置中存在一个或多个错误。</exception>
    public static void Validate(CameraOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Name))
            errors.Add("相机名称不能为空。");

        if (string.IsNullOrWhiteSpace(options.DriverId.Value))
            errors.Add("必须选择相机驱动。");

        if (!IPAddress.TryParse(options.IpAddress, out _))
            errors.Add($"相机 '{options.Name}' 的 IP 地址 '{options.IpAddress}' 无效。");

        if (!Enum.IsDefined(options.ScanType))
            errors.Add($"相机 '{options.Name}' 的扫描类型 '{options.ScanType}' 无效。");

        if (options.ParameterFilePath is not null)
        {
            if (string.IsNullOrWhiteSpace(options.ParameterFilePath))
                errors.Add($"相机 '{options.Name}' 的参数文件路径不能为空白字符串。");
            else if (!File.Exists(options.ParameterFilePath))
                errors.Add($"相机 '{options.Name}' 的参数文件 '{options.ParameterFilePath}' 不存在。");
        }

        if (options.ExposureMilliseconds <= 0)
            errors.Add($"相机 '{options.Name}' 的曝光时间必须大于 0 毫秒。");

        if (options.Gain < 0)
            errors.Add($"相机 '{options.Name}' 的增益不能小于 0。");

        if (options.TimeoutMilliseconds <= 0)
            errors.Add($"相机 '{options.Name}' 的超时时间必须大于 0 毫秒。");

        ValidateTrigger(options.FrameTrigger, "帧开始", options.Name, errors);
        if (options.ScanType == CameraScanType.LineScan)
            ValidateTrigger(options.LineTrigger, "行开始", options.Name, errors);

        if (errors.Count > 0)
            throw new CameraConfigurationException(errors);
    }

    private static void ValidateTrigger(
        CameraTriggerSettings? trigger,
        string triggerName,
        string cameraName,
        ICollection<string> errors)
    {
        if (trigger is null)
        {
            errors.Add($"相机 '{cameraName}' 的{triggerName}触发配置不能为空。");
            return;
        }

        if (trigger.Selector is not null && string.IsNullOrWhiteSpace(trigger.Selector))
            errors.Add($"相机 '{cameraName}' 的{triggerName}触发选择器不能为空白字符串。");

        if (!Enum.IsDefined(trigger.Mode))
            errors.Add($"相机 '{cameraName}' 的{triggerName}触发模式 '{trigger.Mode}' 无效。");

        if (trigger.Mode == CameraTriggerMode.On && string.IsNullOrWhiteSpace(trigger.Source))
            errors.Add($"相机 '{cameraName}' 启用{triggerName}触发时必须指定触发源。");
    }
}
