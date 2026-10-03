// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：DeviceResult。成员说明可结合下方具体实现阅读。
namespace Device.Core.Devices;

/// <summary>
/// 设备生命周期操作的统一结果，避免不同驱动混用布尔值和异常表达普通失败。
/// </summary>
/// <param name="Success">操作是否成功。</param>
/// <param name="ErrorCode">失败时供程序判断的稳定错误码。</param>
/// <param name="Message">供日志或界面展示的错误说明。</param>
public sealed record DeviceResult(bool Success, string? ErrorCode = null, string? Message = null)
{
    /// <summary>创建成功结果。</summary>
    /// <returns>不包含错误信息的成功结果。</returns>
    public static DeviceResult Ok() => new(true);

    /// <summary>创建带稳定错误码的失败结果。</summary>
    /// <param name="errorCode">供程序判断的稳定错误码。</param>
    /// <param name="message">供日志或界面展示的错误说明。</param>
    /// <returns>失败结果。</returns>
    public static DeviceResult Failure(string errorCode, string message) => new(false, errorCode, message);
}
