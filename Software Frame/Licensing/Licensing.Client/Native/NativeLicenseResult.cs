using Extension.Security.Licensing;

namespace Licensing.Native.Client;

/// <summary>
/// 客户端对外暴露的状态码。数值沿用历史原生 ABI 的约定，
/// 既有宿主与测试因此无需改动；与托管核心的对应关系见 <see cref="NativeVerifyResult.Map"/>。
/// </summary>
public enum NativeLicenseStatus
{
    Unavailable = -2, InvalidArgument = -1,
    Valid = 0, InvalidSignature = 1, Malformed = 2, Expired = 3,
    MachineMismatch = 4, FeatureDenied = 5, ClockRollback = 6, TimeStateInvalid = 7, ProductMismatch = 8
}

public sealed record NativeLicenseInfo
{
    public string Id { get; init; } = string.Empty;
    public string ProductId { get; init; } = string.Empty;
    public string Customer { get; init; } = string.Empty;
    public string MachineCode { get; init; } = string.Empty;
    public DateTime ExpireAtUtc { get; init; }
    public bool IsPermanent => ExpireAtUtc >= DateTime.MaxValue.AddDays(-1);

    /// <summary>剩余整天数（不足一天按一天计），由核心在本次校验中一次算出并带出；永久授权为 int.MaxValue。</summary>
    public int RemainingDays { get; init; }
}

public sealed record NativeVerifyResult
{
    public NativeLicenseStatus Status { get; init; }
    public NativeLicenseInfo? License { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool Ok => Status == NativeLicenseStatus.Valid;

    /// <summary>把托管核心的结果转成客户端对外结构，不做任何额外判断或再验签。</summary>
    internal static NativeVerifyResult FromCore(LicenseVerifyResult result) => new()
    {
        Status = Map(result.Status),
        License = result.License is null ? null : new NativeLicenseInfo
        {
            Id = result.License.Id,
            ProductId = result.License.ProductId,
            Customer = result.License.Customer,
            MachineCode = result.License.MachineCode,
            ExpireAtUtc = result.License.ExpireAtUtc,
            RemainingDays = result.RemainingDays
        },
        Message = result.Message
    };

    private static NativeLicenseStatus Map(LicenseStatus status) => status switch
    {
        LicenseStatus.Valid => NativeLicenseStatus.Valid,
        LicenseStatus.InvalidSignature => NativeLicenseStatus.InvalidSignature,
        LicenseStatus.Malformed => NativeLicenseStatus.Malformed,
        LicenseStatus.Expired => NativeLicenseStatus.Expired,
        LicenseStatus.MachineMismatch => NativeLicenseStatus.MachineMismatch,
        LicenseStatus.FeatureDenied => NativeLicenseStatus.FeatureDenied,
        LicenseStatus.ClockRollback => NativeLicenseStatus.ClockRollback,
        LicenseStatus.TimeStateInvalid => NativeLicenseStatus.TimeStateInvalid,
        LicenseStatus.ProductMismatch => NativeLicenseStatus.ProductMismatch,
        _ => NativeLicenseStatus.Unavailable
    };

    /// <summary>仅用于监控自身产生的“不可用”状态，不参与授权判定。</summary>
    internal static NativeVerifyResult Failure(int status) => new()
    {
        Status = Enum.IsDefined(typeof(NativeLicenseStatus), status) && status != 0
            ? (NativeLicenseStatus)status : NativeLicenseStatus.Unavailable,
        Message = status switch
        {
            1 => "授权签名无效，请联系供应商。",
            2 => "授权不存在或格式不支持，请导入 SF2 授权。",
            3 => "软件使用期限已到期，请联系供应商。",
            4 => "授权机器码不匹配。",
            5 => "授权不包含所需功能。",
            6 => "系统时间异常，请校正时间或联系供应商。",
            7 => "授权时间记录或恢复请求不可用，请联系供应商。",
            8 => "授权不属于当前产品，请联系供应商。",
            _ => "授权操作失败，请联系供应商。"
        }
    };
}

public sealed record NativeCheckResult
{
    public NativeVerifyResult Verification { get; init; } = null!;
    public bool ShouldRemind { get; init; }
    public string LicensePath { get; init; } = string.Empty;
}
