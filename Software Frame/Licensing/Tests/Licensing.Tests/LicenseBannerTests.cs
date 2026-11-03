using Licensing.Native.Client;
using Xunit;

namespace Licensing.Tests;

/// <summary>三个宿主共用一处措辞：文案与状态语义在这里钉住，宿主只负责配色。</summary>
public sealed class LicenseBannerTests
{
    private static readonly DateTime ExpireAt = new(2027, 6, 1, 4, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void ValidShowsExpiryAndTheDayCountCarriedByTheResult()
    {
        var banner = LicenseBanner.For(Result(NativeLicenseStatus.Valid, remainingDays: 2));

        Assert.Equal(LicenseBannerState.Valid, banner.State);
        Assert.Contains("授权有效", banner.Text);
        Assert.Contains("剩余 2 天", banner.Text);
        Assert.Contains(ExpireAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), banner.Text);
    }

    [Fact]
    public void PermanentDoesNotPretendToHaveADeadline()
    {
        var banner = LicenseBanner.For(new NativeVerifyResult
        {
            Status = NativeLicenseStatus.Valid,
            License = new NativeLicenseInfo { Customer = "Acme", ExpireAtUtc = DateTime.MaxValue, RemainingDays = int.MaxValue }
        });

        Assert.Equal(LicenseBannerState.Valid, banner.State);
        Assert.Equal("● 授权有效 · 永久 · 客户：Acme", banner.Text);
    }

    [Fact]
    public void ExpiredNamesTheCustomerAndRenewalInsteadOfANotActivatedState()
    {
        var banner = LicenseBanner.For(Result(NativeLicenseStatus.Expired, message: "软件使用期限已到期，请联系供应商。"));

        Assert.Equal(LicenseBannerState.Expired, banner.State);
        Assert.Contains("期限已到期", banner.Text);
        Assert.Contains("续期", banner.Text);
    }

    [Fact]
    public void MissingLicenseIsTheOnlyCaseAllowedToSayNotActivated()
    {
        var banner = LicenseBanner.For(new NativeVerifyResult
        {
            Status = NativeLicenseStatus.Malformed,
            Message = "尚未导入授权，请联系供应商。"
        });

        Assert.Equal(LicenseBannerState.NotActivated, banner.State);
        Assert.StartsWith("● 尚未激活", banner.Text);
    }

    [Fact]
    public void RejectedLicenseSaysUnavailableRatherThanNotActivated()
    {
        var banner = LicenseBanner.For(Result(NativeLicenseStatus.MachineMismatch, message: "授权机器码不匹配。"));

        Assert.Equal(LicenseBannerState.Denied, banner.State);
        Assert.StartsWith("● 授权不可用", banner.Text);
        Assert.DoesNotContain("尚未激活", banner.Text);
    }

    private static NativeVerifyResult Result(NativeLicenseStatus status, int remainingDays = 0, string message = "") => new()
    {
        Status = status,
        Message = message,
        License = new NativeLicenseInfo
        {
            Customer = "Acme",
            ProductId = "PackingMachine.ModelA",
            ExpireAtUtc = ExpireAt,
            RemainingDays = remainingDays
        }
    };
}
