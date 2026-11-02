using System.Windows;
using System.Windows.Threading;
using Extension.Security.Cryptography;
using Licensing.Wpf;
using Xunit;

namespace Licensing.Wpf.Tests;

public sealed class WpfLicenseSessionTests
{
    [Fact]
    public void WpfAdapterCallsManagedAuthorizationCoreDirectly()
    {
        // 不再经过 Native AOT：客户端程序集必须直接引用托管核心，发布客户包才不需要 C++ 链接器。
        Assert.Contains(typeof(WpfLicenseSession).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name == "Licensing.Core");
    }

    [Fact]
    public Task UnloadedWindowCannotStartMonitoring() => RunSta(async () =>
    {
        var window = new Window();
        var (_, key) = RsaEx.GenerateKeyPair();
        Assert.Throws<InvalidOperationException>(() => WpfLicenseSession.Start(window, key, Extension.Security.Licensing.LicenseProduct.DefaultId, (_, _) => Task.CompletedTask));
        window.Close();
        await Task.CompletedTask;
    });

    [Fact]
    public Task DuplicateInitializationIsRejectedAndDisposeStopsMonitoring() => RunSta(async () =>
    {
        var window = await ShowWindow();
        var (_, key) = RsaEx.GenerateKeyPair();
        var firstCheck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = WpfLicenseSession.Start(window, key, Extension.Security.Licensing.LicenseProduct.DefaultId, (result, _) =>
        {
            Assert.False(result.Ok); // 临时随机公钥没有安装授权，不能放行。
            firstCheck.TrySetResult();
            return Task.CompletedTask;
        });
        try
        {
            Assert.Throws<InvalidOperationException>(() => WpfLicenseSession.Start(window, key, Extension.Security.Licensing.LicenseProduct.DefaultId, (_, _) => Task.CompletedTask));
            await firstCheck.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(session.Current.Verification.Ok);
            Assert.False((await session.CheckNowAsync()).Verification.Ok);
        }
        finally
        {
            await session.DisposeAsync();
            window.Close();
        }
        Assert.True(session.Completion.IsCompletedSuccessfully);
        Assert.False(session.Current.Verification.Ok);
        await session.DisposeAsync();
    });

    [Fact]
    public Task ClosingOwnerAutomaticallyStopsMonitoring() => RunSta(async () =>
    {
        var window = await ShowWindow();
        var (_, key) = RsaEx.GenerateKeyPair();
        var firstCheck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = WpfLicenseSession.Start(window, key, Extension.Security.Licensing.LicenseProduct.DefaultId, (_, _) =>
        {
            firstCheck.TrySetResult();
            return Task.CompletedTask;
        });
        try
        {
            await firstCheck.Task.WaitAsync(TimeSpan.FromSeconds(10));
            window.Close();
            await session.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            await session.DisposeAsync();
            Assert.False(session.Current.Verification.Ok);
            Assert.Null(session.ShutdownError);
        }
        finally
        {
            await session.DisposeAsync();
            if (window.IsVisible) window.Close();
        }
    });

    private static async Task<Window> ShowWindow()
    {
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var window = new Window { Width = 160, Height = 100, ShowInTaskbar = false };
        window.Loaded += (_, _) => loaded.TrySetResult();
        window.Show();
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        return window;
    }

    internal static async Task RunSta(Func<Task> body)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    await body();
                    finished.TrySetResult();
                }
                catch (Exception ex) { finished.TrySetException(ex); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "STA Dispatcher did not exit.");
    }
}
