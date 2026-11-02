namespace Extension.Security.Licensing;

/// <summary>验签与时间检查的组合结果；提醒展示由 UI 负责。</summary>
public readonly record struct LicenseCheckResult(LicenseVerifyResult Verification, bool ShouldRemind);
