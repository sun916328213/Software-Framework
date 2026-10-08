using System.Collections.Concurrent;
using System.Reflection;
using DataManager.Controls.Services;
using DataManager.Core.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Workbench.Models;

namespace DataManager.Workbench.Tests;

public class SqlWorkspaceDrainTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UiDrainFinishesWithoutPumpingProviderContinuationAsync(bool replaceProfile)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProvider = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var draining = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var allowDrain = new ManualResetEventSlim();
        var context = new QueuedUiContext();
        SynchronizationContext? providerContext = null;
        var creates = 0;
        async Task<IEnumerable<TestEntity>> QueryAsync()
        {
            providerContext = SynchronizationContext.Current;
            entered.TrySetResult();
            // Like the built-in providers, this await intentionally captures the caller's context.
            await releaseProvider.Task;
            return [];
        }
        var db = Proxy<ISQL<TestEntity>>(method => method.Name == "QueryAsync" ? QueryAsync() : null);
        var workspace = new SqlWorkspace<TestEntity>(Proxy<ISQLManager>(method =>
        {
            if (method.Name != "Create") return true;
            Interlocked.Increment(ref creates);
            return db;
        }));
        await workspace.ConnectAsync();
        Task? pending = null;
        var ui = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                pending = workspace.QueryAllAsync();
                if (!allowDrain.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                draining.TrySetResult();
                if (replaceProfile) workspace.EnsureDb(new SQLParam { DBName = "replacement" });
                else workspace.Disconnect();
                finished.TrySetResult();
            }
            catch (Exception ex) { finished.TrySetException(ex); }
            finally { SynchronizationContext.SetSynchronizationContext(null); }
        }) { IsBackground = true };
        ui.Start();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            allowDrain.Set();
            await draining.Task.WaitAsync(TimeSpan.FromSeconds(10));
            releaseProvider.TrySetResult();
            Assert.Same(finished.Task, await Task.WhenAny(finished.Task, Task.Delay(1000)));
            await finished.Task;
            Assert.Null(providerContext);
            Assert.Equal(replaceProfile ? 2 : 1, creates);
        }
        finally
        {
            releaseProvider.TrySetResult();
            allowDrain.Set();
            // Only failed-test cleanup pumps captured callbacks, so RED cannot strand the host.
            await Task.Run(() =>
            {
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!ui.Join(20) && DateTime.UtcNow < deadline) context.DrainPosted();
            });
            Assert.False(ui.IsAlive);
            if (pending != null) await pending.WaitAsync(TimeSpan.FromSeconds(10));
            workspace.Dispose();
        }
    }

    private sealed class QueuedUiContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _posted = new();
        public override void Post(SendOrPostCallback callback, object? state) => _posted.Enqueue((callback, state));
        public void DrainPosted()
        {
            while (_posted.TryDequeue(out var work)) work.Callback(work.State);
        }
    }

    [Fact]
    public async Task AsyncDisposalReturnsWhileIdleProviderDisconnectIsPending()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var db = Proxy<ISQL<TestEntity>>(method =>
        {
            if (method.Name == "DisConnect")
            {
                entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            }
            return null;
        });
        var workspace = new SqlWorkspace<TestEntity>(Proxy<ISQLManager>(method => method.Name == "Create" ? db : true));
        await workspace.ConnectAsync();
        var close = Task.Run(async () =>
        {
            var pending = workspace.DisposeAsync();
            returned.TrySetResult();
            await pending;
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Same(returned.Task, await Task.WhenAny(returned.Task, Task.Delay(1000)));
        }
        finally { release.Set(); await close.WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisconnectOrDisposeWaitsForEnteredConnect(bool dispose)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var connecting = false;
        var overlap = false;
        var connected = false;
        var db = Proxy<ISQL<TestEntity>>(method =>
        {
            switch (method.Name)
            {
                case "Connect":
                    connecting = true; entered.TrySetResult();
                    if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                    connected = true; connecting = false; return null;
                case "DisConnect": overlap |= connecting; connected = false; return null;
                case "Dispose": overlap |= connecting; return null;
                default: throw new NotSupportedException(method.Name);
            }
        });
        var manager = Proxy<ISQLManager>(method => method.Name == "Create" ? db : method.Name == "Remove" ? true : null);
        var workspace = new SqlWorkspace<TestEntity>(manager);
        var connect = workspace.ConnectAsync();
        Task? closing = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            closing = Task.Run(() => { if (dispose) workspace.Dispose(); else workspace.Disconnect(); });
            Assert.NotSame(closing, await Task.WhenAny(closing, Task.Delay(300)));
        }
        finally
        {
            release.Set();
            await connect.WaitAsync(TimeSpan.FromSeconds(10));
            if (closing != null) await closing.WaitAsync(TimeSpan.FromSeconds(10));
            workspace.Dispose();
        }
        Assert.False(overlap);
        Assert.False(connected);
        Assert.False(workspace.Model.IsConnected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncDisposalDrainsNonCooperativeQueryAndSurvivesFailure(bool fail)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var query = new TaskCompletionSource<IEnumerable<TestEntity>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closes = 0;
        var disposals = 0;
        var db = Proxy<ISQL<TestEntity>>(method => method.Name switch
        {
            "Connect" => null,
            "QueryAsync" => EnterQuery(),
            "DisConnect" => Close(),
            "Dispose" => Dispose(),
            _ => throw new NotSupportedException(method.Name)
        });
        object EnterQuery() { entered.TrySetResult(); return query.Task; }
        object? Close() { closes++; return null; }
        object? Dispose() { disposals++; return null; }
        var workspace = new SqlWorkspace<TestEntity>(Proxy<ISQLManager>(method => method.Name == "Create" ? db : true));
        await workspace.ConnectAsync();
        var pending = workspace.QueryAllAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var first = workspace.DisposeAsync().AsTask();
        var second = workspace.DisposeAsync().AsTask();
        try
        {
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
            Assert.Equal(0, closes);
        }
        finally
        {
            if (fail) query.TrySetException(new IOException("provider-query-fault"));
            else query.TrySetResult([new TestEntity { Name = "late" }]);
            if (fail) await Assert.ThrowsAsync<IOException>(() => pending);
            else await pending.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(1, closes);
        Assert.Equal(1, disposals);
        Assert.Empty(workspace.Model.Results);
    }

    private static T Proxy<T>(Func<MethodInfo, object?> action) where T : class
    {
        var proxy = DispatchProxy.Create<T, ProviderProxy>();
        ((ProviderProxy)(object)proxy).Action = action;
        return proxy;
    }

    public class ProviderProxy : DispatchProxy
    {
        public Func<MethodInfo, object?> Action { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Action(targetMethod!);
    }
}
