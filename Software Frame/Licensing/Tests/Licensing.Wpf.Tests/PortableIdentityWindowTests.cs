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
            Forget(original); Forget(reopened);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    });

    [Fact]
    public Task UnlockDialogRemembersThisMachineAndSkipsThePasswordOnNextStart() => WpfLicenseSessionTests.RunSta(async () =>
    {
        string root = Path.Combine(Path.GetTempPath(), "PortableIdentityUi-" + Guid.NewGuid().ToString("N"));
        using var original = new IssuerEnvironment(root);
        using var restarted = new IssuerEnvironment(root);
        IssuerEnvironmentWindow? window = null;
        try
        {
            using var identity = original.Initialize("correct-portable-password");
            original.ClearAutoUnlock();
            Assert.False(restarted.IsAutoUnlockRemembered);
            window = new IssuerEnvironmentWindow(restarted);
            window.Show();
            var password = (PasswordBox)LogicalTreeHelper.FindLogicalNode(window, "IdentityPassword");
            var remember = (CheckBox)LogicalTreeHelper.FindLogicalNode(window, "RememberUnlock");
            var unlock = (Button)LogicalTreeHelper.FindLogicalNode(window, "UnlockIdentity");
            // 日常零输入是默认；不勾选就维持"每次启动都要密码"的旧行为。
            Assert.True(remember.IsChecked);
            password.Password = "correct-portable-password";
            unlock.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => !window.IsVisible);
            Assert.True(restarted.IsAutoUnlockRemembered);
            using var nextStart = new IssuerEnvironment(root);
            using var auto = nextStart.Load();
            Assert.Equal(identity.PublicKey, auto!.PublicKey);

            // 取消勾选就是原来的行为：这次输入正确密码也不写缓存，下次启动照样要密码。
            restarted.ClearAutoUnlock();
            window.Close();
            using var declined = new IssuerEnvironment(root);
            window = new IssuerEnvironmentWindow(declined);
            window.Show();
            var secondRemember = (CheckBox)LogicalTreeHelper.FindLogicalNode(window, "RememberUnlock");
            var secondUnlock = (Button)LogicalTreeHelper.FindLogicalNode(window, "UnlockIdentity");
            var secondPassword = (PasswordBox)LogicalTreeHelper.FindLogicalNode(window, "IdentityPassword");
            secondRemember.IsChecked = false;
            secondPassword.Password = "correct-portable-password";
            secondUnlock.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => !window.IsVisible);
            Assert.False(declined.IsAutoUnlockRemembered);
        }
        finally
        {
            if (window?.IsVisible == true) window.Close();
            Forget(original); Forget(restarted);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    });

    [Fact]
    public Task UnlockedWindowTurnsAutoUnlockOffWithoutTouchingTheIdentity() => WpfLicenseSessionTests.RunSta(async () =>
    {
        string root = Path.Combine(Path.GetTempPath(), "PortableIdentityUi-" + Guid.NewGuid().ToString("N"));
        IssuerEnvironmentWindow? window = null;
        try
        {
            using var environment = new IssuerEnvironment(root);
            using var identity = environment.Initialize("correct-portable-password");
            window = new IssuerEnvironmentWindow(environment);
            window.Show();
            var status = (TextBlock)LogicalTreeHelper.FindLogicalNode(window, "IdentityStatus");
            var clear = (Button)LogicalTreeHelper.FindLogicalNode(window, "ClearAutoUnlockButton");
            clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => status.Text.Contains("已关闭本机自动解锁"));
            Assert.False(environment.IsAutoUnlockRemembered);
            Assert.Null(LogicalTreeHelper.FindLogicalNode(window, "ClearAutoUnlockButton"));
            // 关的是这台机器的免密入口，不是身份本身：本次会话照样能签发。
            Assert.True(environment.IsUnlocked);
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
            Forget(original); Forget(reopened);
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
        using var environment = new IssuerEnvironment(root);
        try
        {
            using (environment.Initialize("correct-portable-password")) { }
            window = new IssuerEnvironmentWindow(environment);
            window.Show();
            var status = (TextBlock)LogicalTreeHelper.FindLogicalNode(window, "IdentityStatus");
            Assert.NotNull(LogicalTreeHelper.FindLogicalNode(window, "BackupButton"));
            Assert.Null(LogicalTreeHelper.FindLogicalNode(window, "InitializeButton"));
            Assert.Null(LogicalTreeHelper.FindLogicalNode(window, "UnlockIdentity"));
            // 自动解锁开着时给出显式关闭入口，用户能看到这台机器记住了什么。
            Assert.NotNull(LogicalTreeHelper.FindLogicalNode(window, "ClearAutoUnlockButton"));
            Assert.Contains("已解锁", status.Text);
        }
        finally
        {
            if (window?.IsVisible == true) window.Close();
            Forget(environment);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
        return Task.CompletedTask;
    });

    /// <summary>自动解锁缓存写在 %LOCALAPPDATA%，不随测试临时目录删除；不清就会在开发机上越堆越多。</summary>
    private static void Forget(IssuerEnvironment environment)
    {
        try { environment.ClearAutoUnlock(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

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
