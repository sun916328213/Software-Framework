namespace Device.Core.Lights;

/// <summary>
/// 集中维护所有光源控制器共享的配置约束。
/// </summary>
public static class LightControllerOptionsValidator
{
    /// <summary>验证配置；失败时一次返回全部问题。</summary>
    /// <param name="options">待验证的配置。</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="LightControllerConfigurationException">配置包含一个或多个错误。</exception>
    public static void Validate(LightControllerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Name))
            errors.Add("光源控制器名称不能为空。");
        if (string.IsNullOrWhiteSpace(options.DriverId.Value))
            errors.Add("光源控制器驱动标识不能为空。");
        if (string.IsNullOrWhiteSpace(options.PortName))
            errors.Add("光源控制器串口名称不能为空。");
        else if (!IsComPort(options.PortName))
            errors.Add($"光源控制器串口 '{options.PortName}' 必须使用 COM 加正整数的格式。");
        if (options.ChannelCount is < 1 or > 256)
            errors.Add("光源控制器通道数量必须在 1 到 256 之间。");
        if (options.MaximumIntensity is < 1 or > 255)
            errors.Add("光源控制器最大亮度必须在 1 到 255 之间。");

        if (errors.Count > 0)
            throw new LightControllerConfigurationException(errors);
    }

    private static bool IsComPort(string portName)
    {
        var normalized = portName.Trim();
        return normalized.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(normalized.AsSpan(3), out var portNumber)
            && portNumber > 0;
    }
}
