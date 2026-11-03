namespace Licensing.Native.Client;

/// <summary>授权摘要的语义。宿主只按它映射自己的前景色，不要再各自判断状态。</summary>
public enum LicenseBannerState
{
    /// <summary>有效，可放行新任务。</summary>
    Valid,
    /// <summary>期限已过。</summary>
    Expired,
    /// <summary>本机还没有可用授权文本。</summary>
    NotActivated,
    /// <summary>有授权但被拒绝：签名、机器、产品或时间异常。</summary>
    Denied
}

/// <summary>
/// 授权状态摘要文案。激活工具、Workbench 和生产宿主共用这一处措辞，宿主只负责配色。
/// 只产文本、不引主题包：皮肤属于宿主，授权模块不该被 MaterialDesign 版本绑住。
/// </summary>
public sealed record LicenseBanner(LicenseBannerState State, string Text)
{
    public static LicenseBanner For(NativeVerifyResult result)
    {
        NativeLicenseInfo? info = result.License;
        if (result.Ok)
            return info!.IsPermanent
                ? new(LicenseBannerState.Valid, $"● 授权有效 · 永久 · 客户：{info.Customer}")
                : new(LicenseBannerState.Valid,
                    $"● 授权有效 · 到期 {info.ExpireAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}（剩余 {info.RemainingDays} 天）· 客户：{info.Customer}");
        if (result.Status == NativeLicenseStatus.Expired)
            return new(LicenseBannerState.Expired, info == null
                ? "● 期限已到期：" + result.Message
                : $"● 期限已到期（{info.ExpireAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}）· 客户：{info.Customer}，请联系供应商续期。");
        if (result.Status == NativeLicenseStatus.Malformed)
            return new(LicenseBannerState.NotActivated, "● 尚未激活：" + result.Message);
        return new(LicenseBannerState.Denied, "● 授权不可用：" + result.Message);
    }
}
