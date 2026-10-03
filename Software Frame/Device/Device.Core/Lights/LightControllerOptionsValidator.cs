// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LightControllerOptionsValidator。成员说明可结合下方具体实现阅读。
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 IsComPort。
    /// 正文中的关键协作调用包括 Trim、StartsWith、TryParse、AsSpan；具体条件和先后次序见方法体。
    /// </summary>
    private static bool IsComPort(string portName)
    {
        var normalized = portName.Trim();
        return normalized.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(normalized.AsSpan(3), out var portNumber)
            && portNumber > 0;
    }
}
