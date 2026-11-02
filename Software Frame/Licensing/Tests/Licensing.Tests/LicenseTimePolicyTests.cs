using Extension.Security.Licensing;
using Xunit;

namespace Licensing.Tests;

public class LicenseTimePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void TimedLicense_ExpiresAtExactDeadline()
    {
        var license = new LicenseInfo { ExpireAtUtc = Now.UtcDateTime };
        Assert.Equal(LicenseTimeStatus.Expired, LicenseTimePolicy.Evaluate(license, Now, null, null).Status);
    }

    [Fact]
    public void TimedLicense_RemindsOnlyWithinThirtyDaysAndOncePerLocalDay()
    {
        var license = new LicenseInfo { ExpireAtUtc = Now.UtcDateTime.AddDays(30) };
        var today = DateOnly.FromDateTime(Now.DateTime);
        Assert.True(LicenseTimePolicy.Evaluate(license, Now, null, null).ShouldRemind);
        Assert.False(LicenseTimePolicy.Evaluate(license, Now, null, today).ShouldRemind);
        Assert.True(LicenseTimePolicy.Evaluate(license, Now, null, today.AddDays(-1)).ShouldRemind);
        license.ExpireAtUtc = Now.UtcDateTime.AddDays(30).AddSeconds(1);
        Assert.False(LicenseTimePolicy.Evaluate(license, Now, null, null).ShouldRemind);
    }

    [Fact]
    public void TimedLicense_RejectsRollbackWithoutLoweringHighWaterMark()
    {
        var license = new LicenseInfo { ExpireAtUtc = Now.UtcDateTime.AddDays(10) };
        var highest = Now.AddMinutes(1);
        var result = LicenseTimePolicy.Evaluate(license, Now, highest, null);
        Assert.Equal(LicenseTimeStatus.ClockRollback, result.Status);
        Assert.Equal(highest.ToUniversalTime(), result.HighestObservedUtc);
        Assert.False(result.ShouldRemind);
        Assert.Equal(LicenseTimeStatus.Valid, LicenseTimePolicy.Evaluate(license, highest, highest, null).Status);
    }

    [Fact]
    public void PermanentLicense_DoesNotDependOnClockOrRemind()
    {
        var license = new LicenseInfo { ExpireAtUtc = DateTime.MaxValue };
        var result = LicenseTimePolicy.Evaluate(license, Now, Now.AddDays(1), null);
        Assert.Equal(LicenseTimeStatus.Valid, result.Status);
        Assert.False(result.ShouldRemind);
    }

    [Fact]
    public void TimedLicense_RejectsUnspecifiedExpiryKind()
    {
        var license = new LicenseInfo { ExpireAtUtc = DateTime.SpecifyKind(Now.DateTime.AddDays(1), DateTimeKind.Unspecified) };
        Assert.Throws<ArgumentException>(() => LicenseTimePolicy.Evaluate(license, Now, null, null));
    }

    [Fact]
    public void RemainingDays_CountsAPartialDayAsOneDay()
    {
        // 剩 1 天零 3 小时：截断除法的 1 与向上取整的 2 曾让界面和日志各显示一个剩余时间。
        Assert.Equal(2, LicenseTimePolicy.RemainingDays(Now.UtcDateTime.AddDays(1).AddHours(3), Now.UtcDateTime));
        Assert.Equal(1, LicenseTimePolicy.RemainingDays(Now.UtcDateTime.AddSeconds(30), Now.UtcDateTime));
        Assert.Equal(0, LicenseTimePolicy.RemainingDays(Now.UtcDateTime, Now.UtcDateTime));
        Assert.Equal(0, LicenseTimePolicy.RemainingDays(Now.UtcDateTime.AddDays(1), Now.UtcDateTime.AddDays(2)));
    }
}
