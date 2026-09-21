namespace Device.Core.LabelPrinting;

/// <summary>
/// 集中维护标签打印软件共享的配置约束。
/// </summary>
public static class LabelPrintEngineOptionsValidator
{
    /// <summary>验证配置；失败时一次返回全部问题。</summary>
    public static void Validate(LabelPrintEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Name))
            errors.Add("标签打印引擎名称不能为空。");
        if (string.IsNullOrWhiteSpace(options.DriverId.Value))
            errors.Add("标签打印引擎驱动标识不能为空。");
        if (options.PrintTimeoutMilliseconds <= 0)
            errors.Add("标签打印超时时间必须大于 0 毫秒。");
        if (options.PreviewDpi is < 72 or > 1_200)
            errors.Add("标签预览 DPI 必须在 72 到 1200 之间。");

        if (errors.Count > 0)
            throw new LabelPrintEngineConfigurationException(errors);
    }
}
