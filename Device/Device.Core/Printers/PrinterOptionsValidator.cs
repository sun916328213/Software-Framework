using System.Net;

namespace Device.Core.Printers;

/// <summary>
/// 集中维护网络打印机共享的配置约束。
/// </summary>
public static class PrinterOptionsValidator
{
    /// <summary>验证配置；失败时一次返回全部问题。</summary>
    /// <param name="options">待验证的打印机配置。</param>
    /// <exception cref="PrinterConfigurationException">配置包含一个或多个错误。</exception>
    public static void Validate(PrinterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Name))
            errors.Add("打印机名称不能为空。");
        if (string.IsNullOrWhiteSpace(options.DriverId.Value))
            errors.Add("打印机驱动标识不能为空。");
        if (!IPAddress.TryParse(options.IpAddress, out _))
            errors.Add($"打印机 IP 地址 '{options.IpAddress}' 无效。");
        if (options.Port is < 1 or > 65_535)
            errors.Add("打印机 TCP 端口必须在 1 到 65535 之间。");
        if (options.MaximumReadTimeoutMilliseconds <= 0)
            errors.Add("打印机最大读取超时必须大于 0 毫秒。");
        if (options.TimeToWaitForMoreDataMilliseconds <= 0)
            errors.Add("打印机后续数据等待时间必须大于 0 毫秒。");
        if (options.TimeToWaitForMoreDataMilliseconds > options.MaximumReadTimeoutMilliseconds)
            errors.Add("打印机后续数据等待时间不能超过最大读取超时。");

        if (errors.Count > 0)
            throw new PrinterConfigurationException(errors);
    }
}
