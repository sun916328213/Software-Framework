namespace Extension.Security.Licensing;

/// <summary>授权时间检查的结果；不表示签名或机器码已验证。</summary>
public enum LicenseTimeStatus
{
    Valid,
    Expired,
    ClockRollback
}

/// <summary>时间检查结果。历史状态由调用方安全保存并在提示显示后更新。</summary>
public readonly record struct LicenseTimeResult(
    LicenseTimeStatus Status,
    DateTimeOffset HighestObservedUtc,
    bool ShouldRemind);

/// <summary>
/// 纯时间规则，不负责验证签名、持久化、定时调度或防止本地状态被删除和回滚。
/// 只应在授权签名与机器码验证成功后使用。
/// </summary>
public static class LicenseTimePolicy
{
    public static LicenseTimeResult Evaluate(
        LicenseInfo license,
        DateTimeOffset now,
        DateTimeOffset? highestObservedUtc,
        DateOnly? lastReminderLocalDate)
    {
        ArgumentNullException.ThrowIfNull(license);
        var nowUtc = now.ToUniversalTime();
        var highest = highestObservedUtc?.ToUniversalTime();

        // 永久授权不依赖墙上时钟，不应因校时而失效。
        if (license.IsPermanent)
            return new(LicenseTimeStatus.Valid, highest ?? nowUtc, false);

        if (highest.HasValue && nowUtc < highest.Value)
            return new(LicenseTimeStatus.ClockRollback, highest.Value, false);

        var expire = license.ExpireAtUtc;
        if (expire.Kind != DateTimeKind.Utc)
            throw new ArgumentException("限时授权的到期时间必须为 UTC。", nameof(license));

        if (nowUtc.UtcDateTime >= expire)
            return new(LicenseTimeStatus.Expired, nowUtc, false);

        bool withinReminderWindow = expire - nowUtc.UtcDateTime <= TimeSpan.FromDays(30);
        var localDate = DateOnly.FromDateTime(now.DateTime);
        bool shouldRemind = withinReminderWindow &&
                            (!lastReminderLocalDate.HasValue || localDate > lastReminderLocalDate.Value);
        return new(LicenseTimeStatus.Valid, nowUtc, shouldRemind);
    }

    /// <summary>
    /// 剩余整天数，不足一天按一天计；已到期返回 0。
    /// 界面摘要和校验日志必须走这一个算法 —— 两处各算一遍就会出现同一张授权显示两个剩余时间。
    /// </summary>
    public static int RemainingDays(DateTime expireAtUtc, DateTime nowUtc)
    {
        TimeSpan left = expireAtUtc - nowUtc;
        return left <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(left.TotalDays);
    }
}
