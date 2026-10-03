// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LabelPrintEngineOptionsValidator。成员说明可结合下方具体实现阅读。
namespace Device.Core.LabelPrinting;

/// <summary>
/// 集中维护标签打印软件共享的配置约束。
/// </summary>
public static class LabelPrintEngineOptionsValidator
{
    /// <summary>验证配置；失败时一次返回全部问题。</summary>
    /// <param name="options">待验证的标签打印引擎配置。</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="LabelPrintEngineConfigurationException">配置包含一个或多个错误。</exception>
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
