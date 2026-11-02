using System.Windows;
using Extension.Security.Cryptography;
using System.Windows.Controls;
using Xunit;

namespace Licensing.Wpf.Tests;

public sealed class ActivationWindowTests
{
    [Fact]
    public Task ConfiguredClientEnablesActualImportAndRecovery() => WpfLicenseSessionTests.RunSta(() =>
    {
        var (_, publicKey) = RsaEx.GenerateKeyPair();
        var window = new Licensing.Activation.MainWindow(publicKey, "Activation.Test." + Guid.NewGuid().ToString("N"));
        try
        {
            var activate = (Button)window.FindName("ActivateButton");
            Assert.True(activate.IsEnabled);
            Assert.Equal("激活", activate.Content);
            Assert.NotNull(window.FindName("LicenseInput"));
            Assert.True(((Button)window.FindName("RecoveryButton")).IsEnabled);
            Assert.True(((Button)window.FindName("ClearButton")).IsEnabled);
            Assert.Contains("尚未导入授权", ((TextBox)window.FindName("Status")).Text);
            // 常驻摘要必须直接说清当前状态，而不是只在日志区里。
            Assert.Contains("尚未激活", ((TextBlock)window.FindName("LicenseSummary")).Text);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });

    [Fact]
    public Task MissingConfigurationKeepsActivationClickableAndExplainsWhy() => WpfLicenseSessionTests.RunSta(() =>
    {
        var window = new Licensing.Activation.MainWindow(null, null);
        try
        {
            var activate = (Button)window.FindName("ActivateButton");
            // 未配置时主按钮不置灰：点了弹说明框，而不是让人以为程序坏了。
            Assert.True(activate.IsEnabled);
            // 不触发 Click：会弹出模态说明框，测试线程会被卡住。
            Assert.Contains("生成客户工具包", ((TextBox)window.FindName("Status")).Text);
            // 措辞必须是“开发构建/本机调试”，不能说成“无法激活”，否则像故障而不是分工。
            Assert.Contains("开发构建", ((TextBlock)window.FindName("LicenseSummary")).Text);
            Assert.Contains("不是给客户的发行包", ((TextBox)window.FindName("Status")).Text);
            Assert.False(((Button)window.FindName("RecoveryButton")).IsEnabled);
            Assert.False(((Button)window.FindName("ClearButton")).IsEnabled);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });
}
