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
}
