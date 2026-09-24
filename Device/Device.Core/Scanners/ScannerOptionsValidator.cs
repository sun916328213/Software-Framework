namespace Device.Core.Scanners;

/// <summary>
/// 集中维护所有串口扫码器共享的配置约束。
/// </summary>
public static class ScannerOptionsValidator
{
    private static readonly HashSet<string> SupportedParity =
        new(StringComparer.OrdinalIgnoreCase) { "none", "odd", "even", "mark", "space" };

    /// <summary>验证配置；失败时一次返回全部问题。</summary>
    /// <param name="options">待验证的配置。</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ScannerConfigurationException">配置包含一个或多个错误。</exception>
    public static void Validate(ScannerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Name))
            errors.Add("扫码器名称不能为空。");
        if (string.IsNullOrWhiteSpace(options.DriverId.Value))
            errors.Add("扫码器驱动标识不能为空。");
        if (string.IsNullOrWhiteSpace(options.PortName))
            errors.Add("扫码器串口名称不能为空。");
        else if (!IsComPort(options.PortName))
            errors.Add($"扫码器串口 '{options.PortName}' 必须使用 COM 加正整数的格式。");
        if (options.BaudRate <= 0)
            errors.Add("扫码器波特率必须大于 0。");
        if (options.DataBits is < 5 or > 8)
            errors.Add("扫码器数据位必须在 5 到 8 之间。");
        if (options.StopBits is not (1 or 2))
            errors.Add("扫码器停止位必须为 1 或 2。");
        if (string.IsNullOrWhiteSpace(options.Parity) || !SupportedParity.Contains(options.Parity))
            errors.Add("扫码器校验方式必须为 none、odd、even、mark 或 space。");
        if (string.IsNullOrEmpty(options.Delimiter))
            errors.Add("扫码器数据结束符不能为空。");
        if (options.MaximumFrameBytes <= 0)
            errors.Add("扫码器最大帧长度必须大于 0。");
        if (options.TimeoutMilliseconds <= 0)
            errors.Add("扫码器超时时间必须大于 0。");

        if (errors.Count > 0)
            throw new ScannerConfigurationException(errors);
    }

    private static bool IsComPort(string? portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
            return false;

        var normalized = portName.Trim();
        return normalized.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(normalized.AsSpan(3), out var portNumber)
            && portNumber > 0;
    }
}
