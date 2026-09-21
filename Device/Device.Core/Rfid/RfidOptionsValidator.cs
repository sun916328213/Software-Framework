using Device.Core.Rfid;

namespace Device.Core.Rfid;

/// <summary>
/// 集中维护所有串口 RFID 设备共享的配置约束。
/// </summary>
public static class RfidOptionsValidator
{
    private static readonly HashSet<string> SupportedParity =
        new(StringComparer.OrdinalIgnoreCase) { "none", "odd", "even", "mark", "space" };

    /// <summary>验证配置；失败时一次返回全部问题。</summary>
    /// <param name="options">待验证的配置。</param>
    /// <exception cref="RfidConfigurationException">配置包含一个或多个错误。</exception>
    public static void Validate(RfidOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Name))
            errors.Add("RFID 名称不能为空。");
        if (string.IsNullOrWhiteSpace(options.DriverId.Value))
            errors.Add("RFID 驱动标识不能为空。");
        if (!IsComPort(options.PortName))
            errors.Add($"RFID 串口 '{options.PortName}' 必须使用 COM 加正整数的格式。");
        if (options.BaudRate <= 0)
            errors.Add("RFID 波特率必须大于 0。");
        if (options.DataBits is < 5 or > 8)
            errors.Add("RFID 数据位必须在 5 到 8 之间。");
        if (options.StopBits is not (1 or 2))
            errors.Add("RFID 停止位必须为 1 或 2。");
        if (string.IsNullOrWhiteSpace(options.Parity) || !SupportedParity.Contains(options.Parity))
            errors.Add("RFID 校验方式必须为 none、odd、even、mark 或 space。");
        if (options.SlaveId == 0)
            errors.Add("RFID Modbus 从站地址必须大于 0。");
        if (options.PageSizeBytes != 8)
            errors.Add("当前 RFID 驱动每页固定为 8 字节。");
        if (options.TimeoutMilliseconds <= 0)
            errors.Add("RFID 超时时间必须大于 0。");

        if (errors.Count > 0)
            throw new RfidConfigurationException(errors);
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
