using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using Extension.Security.Cryptography;
using Extension.Security.Licensing;
using Licensing.Native.Client;
using Xunit;
using HostWindow = Licensing.Workbench.MainWindow;

namespace Licensing.Wpf.Tests;

public sealed class WorkbenchTests
{
    [Fact]
    public Task MissingConfigurationCannotStartTasks() => WpfLicenseSessionTests.RunSta(async () =>
    {
        var window = new HostWindow(null, null);
        try
        {
            window.Show();
            await WaitUntil(() => window.IsLoaded);
            Assert.False(((Button)window.FindName("StartTask")).IsEnabled);
            Assert.False(await window.RunSimulationAsync(TimeSpan.Zero));
            Assert.Equal(0, window.StartedTasks);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task ExpiryDoesNotInterruptAnAdmittedTaskButRejectsTheNextOne() => WpfLicenseSessionTests.RunSta(async () =>
    {
        using var installed = new InstalledLicense();
        installed.Import(DateTime.UtcNow.AddSeconds(4));
        var window = new HostWindow(installed.PublicKey, installed.Product, TimeSpan.FromMilliseconds(40));
        try
        {
            window.Show();
            await WaitUntil(() => ((Button)window.FindName("StartTask")).IsEnabled);
            var task = window.RunSimulationAsync(TimeSpan.FromSeconds(6));
            await WaitUntil(() => window.StartedTasks == 1);
            await WaitUntil(() => ((TextBlock)window.FindName("Authorization")).Text.Contains("期限已到期"), TimeSpan.FromSeconds(7));
            Assert.True(window.TaskInProgress);
            Assert.False(((Button)window.FindName("StartTask")).IsEnabled);
            Assert.True(await task.WaitAsync(TimeSpan.FromSeconds(8)));
            Assert.Equal(1, window.CompletedTasks);
            Assert.False(await window.RunSimulationAsync(TimeSpan.Zero));
            Assert.Equal(1, window.StartedTasks);
        }
        finally
        {
            window.Close();
            if (window.MonitorCompletion != null) await window.MonitorCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        }
    });

    [Fact]
    public Task ClosingHostCancelsOnlyItsSimulationAndReleasesMonitoring() => WpfLicenseSessionTests.RunSta(async () =>
    {
        using var installed = new InstalledLicense();
        installed.Import(DateTime.MaxValue);
        var window = new HostWindow(installed.PublicKey, installed.Product, TimeSpan.FromMilliseconds(40));
        try
        {
            window.Show();
            await WaitUntil(() => ((Button)window.FindName("StartTask")).IsEnabled);
            var task = window.RunSimulationAsync(TimeSpan.FromMinutes(1));
            await WaitUntil(() => window.StartedTasks == 1);
            window.Close();
            Assert.False(await task.WaitAsync(TimeSpan.FromSeconds(10)));
            await window.MonitorCompletion!.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, window.CompletedTasks);
        }
        finally
        {
            if (window.IsVisible) window.Close();
            if (window.MonitorCompletion != null) await window.MonitorCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        }
    });

    [Fact]
    public void PublishDirectoryCreatesChildFolderInsteadOfRefusingOrOverwriting()
    {
        string root = Path.Combine(Path.GetTempPath(), "PublishDir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "keep.txt"), "keep");
            // 非空目录：不报错、不删文件，改为在其下新建带时间戳的子目录。
            string resolved = Licensing.Authorization.MainWindow.ResolvePublishDirectory(root, "Probe.Product");
            Assert.NotEqual(root, resolved);
            Assert.True(Directory.Exists(resolved));
            Assert.Empty(Directory.EnumerateFileSystemEntries(resolved));
            Assert.Equal("keep", File.ReadAllText(Path.Combine(root, "keep.txt")));
            // 空目录：直接用，不额外套一层。
            string empty = Path.Combine(root, "empty");
            Directory.CreateDirectory(empty);
            Assert.Equal(empty, Licensing.Authorization.MainWindow.ResolvePublishDirectory(empty, "Probe.Product"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void TrustAnchorPreflightSaysWhyTheCustomerPackageWouldBeRefused()
    {
        string root = Path.Combine(Path.GetTempPath(), "TrustPreflight-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Build"));
        string props = Path.Combine(root, "Build", "Trust.props");
        const string pinned = "1111111111222222222233333333334444444444555555555566666666667777";
        try
        {
            Assert.Contains("仓库信任锚缺失",
                Licensing.Authorization.MainWindow.TrustAnchorStatus(root, pinned));

            File.WriteAllText(props, """
                <Project>
                  <PropertyGroup><LicenseExpectedPublicKeyFingerprint>abc</LicenseExpectedPublicKeyFingerprint></PropertyGroup>
                </Project>
                """);
            Assert.Contains("未钉指纹", Licensing.Authorization.MainWindow.TrustAnchorStatus(root, pinned));

            File.WriteAllText(props, $"""
                <Project>
                  <PropertyGroup>
                    <LicensePublicKeyFile>$(MSBuildThisFileDirectory)Keys\public-{pinned}.key</LicensePublicKeyFile>
                    <LicenseExpectedPublicKeyFingerprint>{pinned}</LicenseExpectedPublicKeyFingerprint>
                  </PropertyGroup>
                </Project>
                """);
            // 指纹对得上却少了钥匙文件，同样会在发布时被拦下，这里要提前讲出来。
            Assert.Contains("缺文件", Licensing.Authorization.MainWindow.TrustAnchorStatus(root, pinned));
            Directory.CreateDirectory(Path.Combine(root, "Build", "Keys"));
            File.WriteAllText(Path.Combine(root, "Build", "Keys", $"public-{pinned}.key"), "placeholder");
            Assert.Contains("可以生成客户包", Licensing.Authorization.MainWindow.TrustAnchorStatus(root, pinned));
            Assert.Contains("信任锚不一致",
                Licensing.Authorization.MainWindow.TrustAnchorStatus(root, "9".PadRight(64, '9')));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public Task VendorWindowDoesNotExposeRawCredentialsOrAutomaticallyCreateThem() => WpfLicenseSessionTests.RunSta(() =>
    {
        string root = Path.Combine(Path.GetTempPath(), "IssuerUi-" + Guid.NewGuid().ToString("N"));
        var environment = new Licensing.Authorization.IssuerEnvironment(root);
        var window = new Licensing.Authorization.MainWindow(environment);
        try
        {
            Assert.Null(window.FindName("TxtPrivateKey"));
            Assert.Null(window.FindName("TxtPublicKey"));
            Assert.False(File.Exists(environment.CredentialPath));
            Assert.Contains("尚未创建签发身份", ((TextBlock)window.FindName("EnvironmentStatus")).Text);
        }
        finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
        return Task.CompletedTask;
    });

    [Fact]
    public Task VendorWindowLaysOutFourPagesWithVisibleIssueFields() => WpfLicenseSessionTests.RunSta(async () =>
    {
        string root = Path.Combine(Path.GetTempPath(), "IssuerLayout-" + Guid.NewGuid().ToString("N"));
        var environment = new Licensing.Authorization.IssuerEnvironment(root);
        var window = new Licensing.Authorization.MainWindow(environment);
        try
        {
            var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.Loaded += (_, _) => loaded.TrySetResult();
            window.Show();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));

            var tabs = (TabControl)window.FindName("Tabs");
            Assert.Equal(4, tabs.Items.Count);
            Assert.Contains("客户工具包", tabs.Items.Cast<TabItem>().Select(tab => (string)tab.Header));
            // 发布从签发页的折叠区移到独立分页：取消按钮若还留在原页，RunAsync 会找不到它。
            Assert.NotNull(window.FindName("CancelOperation"));
            // 紧凑样式改由窗口自己合并：字典没带上时控件会塌成 0 高，只看 x:Name 发现不了。
            foreach (string name in new[] { "Product", "Customer", "Machine", "ExpiryDays" })
                Assert.True(((TextBox)window.FindName(name)).ActualHeight > 0, name);
            Assert.True(((TextBox)window.FindName("StatusText")).ActualHeight > 0);
        }
        finally
        {
            window.Close();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    });

    private static async Task WaitUntil(Func<bool> condition, TimeSpan? timeout = null)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(timer.Elapsed < (timeout ?? TimeSpan.FromSeconds(6)), "Expected UI state was not reached.");
            await Task.Delay(20);
        }
    }

    private sealed class InstalledLicense : IDisposable
    {
        private readonly string _privateKey;
        private readonly string _temporary = Path.Combine(Path.GetTempPath(), "WorkbenchLicense-" + Guid.NewGuid().ToString("N"));
        private readonly string _scope;
        internal string PublicKey { get; }
        internal string Product { get; } = "Workbench.Test." + Guid.NewGuid().ToString("N");
        internal InstalledLicense()
        {
            (_privateKey, PublicKey) = RsaEx.GenerateKeyPair();
            Directory.CreateDirectory(_temporary);
            _scope = Path.GetDirectoryName(new LicenseFileStore(PublicKey, productId: Product).LicensePath)!;
        }
        internal void Import(DateTime expiry)
        {
            string file = Path.Combine(_temporary, "test.lic");
            File.WriteAllText(file, LicenseManagerEx.CreateBound(new LicenseInfo
            {
                ProductId = Product, Customer = "Workbench test", MachineCode = MachineCodeEx.GetMachineCode(), ExpireAtUtc = expiry
            }, _privateKey));
            using var activation = new NativeLicenseSession(PublicKey, Product);
            var result = activation.ImportFile(file);
            Assert.True(result.Ok, result.Message);
        }
        public void Dispose()
        {
            if (Directory.Exists(_scope)) Directory.Delete(_scope, true);
            if (Directory.Exists(_temporary)) Directory.Delete(_temporary, true);
        }
    }
}
