using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Licensing.Authorization;
using Xunit;

namespace Licensing.Wpf.Tests;

public sealed class PortableIdentityWindowTests
{
    [Fact]
    public Task LockedIdentityWindowRejectsWrongPasswordAndClosesAfterUnlock() => WpfLicenseSessionTests.RunSta(async () =>
    {
        string root = Path.Combine(Path.GetTempPath(), "PortableIdentityUi-" + Guid.NewGuid().ToString("N"));
        using var original = new IssuerEnvironment(root);
        using var reopened = new IssuerEnvironment(root);
        IssuerEnvironmentWindow? window = null;
        try
        {
            using var identity = original.Initialize("correct-portable-password");
            window = new IssuerEnvironmentWindow(reopened);
            window.Show();
            var password = (PasswordBox)LogicalTreeHelper.FindLogicalNode(window, "IdentityPassword");
            var status = (TextBlock)LogicalTreeHelper.FindLogicalNode(window, "IdentityStatus");
            var unlock = (Button)LogicalTreeHelper.FindLogicalNode(window, "UnlockIdentity");
            // 已创建但未解锁：只出现“解锁”，不出现“创建”。
            Assert.NotNull(unlock);
            Assert.Null(LogicalTreeHelper.FindLogicalNode(window, "InitializeButton"));
            password.Password = "incorrect-password";
            unlock.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => status.Text.Contains("密码错误"));
            Assert.True(window.IsVisible);
            Assert.False(reopened.IsUnlocked);
            Assert.Empty(password.Password);
            password.Password = "correct-portable-password";
            unlock.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => !window.IsVisible);
            Assert.True(reopened.IsUnlocked);
            using var loaded = reopened.Load();
            Assert.Equal(identity.PublicKey, loaded!.PublicKey);
        }
        finally
        {
            if (window?.IsVisible == true) window.Close();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    });

    [Fact]
    public Task RestoreHasItsOwnBackupPasswordAndStopsBeforeTheFileDialogWithoutIt() => WpfLicenseSessionTests.RunSta(async () =>
    {
        string root = Path.Combine(Path.GetTempPath(), "PortableIdentityUi-" + Guid.NewGuid().ToString("N"));
        using var original = new IssuerEnvironment(root);
        using var reopened = new IssuerEnvironment(root);
        IssuerEnvironmentWindow? window = null;
        try
        {
            using var identity = original.Initialize("correct-portable-password");
            window = new IssuerEnvironmentWindow(reopened);
            window.Show();
            var unlock = (PasswordBox)LogicalTreeHelper.FindLogicalNode(window, "IdentityPassword");
            var backupBox = (PasswordBox)LogicalTreeHelper.FindLogicalNode(window, "RestorePassword");
            var status = (TextBlock)LogicalTreeHelper.FindLogicalNode(window, "IdentityStatus");
            var restore = (Button)LogicalTreeHelper.FindLogicalNode(window, "RestoreButton");
            await Task.Delay(300);
            // 原报告是"按钮不能使用"：恢复入口必须不滚动就完整可见、能命中测试，否则再怎么解释逻辑都对不上用户看到的界面。
            var scroll = (ScrollViewer)window.Content;
            double top = restore.TransformToVisual(scroll).Transform(new Point(0, 0)).Y;
            Assert.True(top >= 0 && top + restore.ActualHeight <= scroll.ActualHeight,
                $"restoreTop={top} height={restore.ActualHeight} viewport={scroll.ActualHeight} scrollable={scroll.ScrollableHeight}");
            Assert.True(restore.IsHitTestVisible);

            // 解锁密码填对也替不了备份密码：恢复要的是这份 .sfib 自己的密码，两者共用一个框时用户只会看到"按钮没反应"。
            unlock.Password = "correct-portable-password";
            restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => status.Text.Contains("备份密码"));
            // 走到这里说明没弹文件选择框（弹了会在这里阻塞），也没顺手解锁身份。
            Assert.Contains("解锁密码解不开备份", status.Text);
            Assert.False(reopened.IsUnlocked);
            Assert.NotNull(backupBox);
            Assert.Null(LogicalTreeHelper.FindLogicalNode(window, "BackupPassword"));
        }
        finally
        {
            if (window?.IsVisible == true) window.Close();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    });

    [Fact]
    public Task UninitializedWindowOffersOnlyCreation() => WpfLicenseSessionTests.RunSta(() =>
    {
        string root = Path.Combine(Path.GetTempPath(), "PortableIdentityUi-" + Guid.NewGuid().ToString("N"));
        using var environment = new IssuerEnvironment(root);
        IssuerEnvironmentWindow? window = null;
        try
        {
            window = new IssuerEnvironmentWindow(environment);
            window.Show();
            var status = (TextBlock)LogicalTreeHelper.FindLogicalNode(window, "IdentityStatus");
            // 没有身份时根本不该出现“解锁”按钮，而不是显示成灰色让人猜。
            Assert.Null(LogicalTreeHelper.FindLogicalNode(window, "UnlockButton"));
            Assert.Null(LogicalTreeHelper.FindLogicalNode(window, "UnlockIdentity"));
            Assert.NotNull(LogicalTreeHelper.FindLogicalNode(window, "InitializeButton"));
            Assert.NotNull(LogicalTreeHelper.FindLogicalNode(window, "RestoreButton"));
            Assert.Contains("还没有签发身份", status.Text);
        }
        finally
        {
            if (window?.IsVisible == true) window.Close();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
        return Task.CompletedTask;
    });

    [Fact]
    public Task UnlockedWindowOffersBackupInsteadOfCreationOrUnlock() => WpfLicenseSessionTests.RunSta(() =>
    {
        string root = Path.Combine(Path.GetTempPath(), "PortableIdentityUi-" + Guid.NewGuid().ToString("N"));
        IssuerEnvironmentWindow? window = null;
        try
        {
            using var environment = new IssuerEnvironment(root);
            using (environment.Initialize("correct-portable-password")) { }
            window = new IssuerEnvironmentWindow(environment);
            window.Show();
            var status = (TextBlock)LogicalTreeHelper.FindLogicalNode(window, "IdentityStatus");
            Assert.NotNull(LogicalTreeHelper.FindLogicalNode(window, "BackupButton"));
            Assert.Null(LogicalTreeHelper.FindLogicalNode(window, "InitializeButton"));
            Assert.Null(LogicalTreeHelper.FindLogicalNode(window, "UnlockIdentity"));
            Assert.Contains("已解锁", status.Text);
        }
        finally
        {
            if (window?.IsVisible == true) window.Close();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
        return Task.CompletedTask;
    });

    private static async Task Until(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(8), "Password dialog did not reach expected state.");
            await Task.Delay(20);
        }
    }
}
