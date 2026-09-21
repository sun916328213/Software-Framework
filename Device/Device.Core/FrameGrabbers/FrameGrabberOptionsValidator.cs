namespace Device.Core.FrameGrabbers;

/// <summary>
/// 集中维护采集卡共享的配置约束。
/// </summary>
public static class FrameGrabberOptionsValidator
{
    /// <summary>验证配置；失败时一次返回全部问题。</summary>
    /// <param name="options">待验证的采集卡配置。</param>
    /// <exception cref="FrameGrabberConfigurationException">配置包含一个或多个错误。</exception>
    public static void Validate(FrameGrabberOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Name))
            errors.Add("采集卡名称不能为空。");
        if (string.IsNullOrWhiteSpace(options.DriverId.Value))
            errors.Add("采集卡驱动标识不能为空。");
        if (string.IsNullOrWhiteSpace(options.ConfigurationFilePath))
            errors.Add("采集卡配置文件路径不能为空。");
        if (options.BoardIndex < 0)
            errors.Add("采集卡索引不能小于 0。");
        if (string.IsNullOrWhiteSpace(options.Connector))
            errors.Add("采集卡连接器名称不能为空。");
        if (string.IsNullOrWhiteSpace(options.PixelFormat))
            errors.Add("采集卡像素格式不能为空。");
        if (options.BufferCount is < 1 or > 16)
            errors.Add("采集卡缓冲区数量必须在 1 到 16 之间。");

        if (errors.Count > 0)
            throw new FrameGrabberConfigurationException(errors);
    }
}
