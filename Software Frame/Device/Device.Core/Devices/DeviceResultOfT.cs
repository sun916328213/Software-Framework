// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：DeviceResult。成员说明可结合下方具体实现阅读。
namespace Device.Core.Devices;

/// <summary>
/// 同时返回业务值和稳定错误信息的设备操作结果。
/// </summary>
/// <typeparam name="T">成功时返回的值类型。</typeparam>
/// <param name="Success">操作是否成功。</param>
/// <param name="Value">成功时的业务值。</param>
/// <param name="ErrorCode">失败时供程序判断的稳定错误码。</param>
/// <param name="Message">供日志或界面展示的错误说明。</param>
public sealed record DeviceResult<T>(
    bool Success,
    T? Value = default,
    string? ErrorCode = null,
    string? Message = null)
{
    /// <summary>创建成功结果。</summary>
    /// <param name="value">操作返回值。</param>
    /// <returns>包含业务值的成功结果。</returns>
    public static DeviceResult<T> Ok(T value) => new(true, value);

    /// <summary>创建带稳定错误码的失败结果。</summary>
    /// <param name="errorCode">供程序判断的稳定错误码。</param>
    /// <param name="message">供日志或界面展示的错误说明。</param>
    /// <returns>不包含业务值的失败结果。</returns>
    public static DeviceResult<T> Failure(string errorCode, string message) =>
        new(false, default, errorCode, message);
}
