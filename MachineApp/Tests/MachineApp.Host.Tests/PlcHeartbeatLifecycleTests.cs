using System.Reflection;
using Communication.Core.Abstractions;
using Communication.Core.Protocols.Mitsubishi;
using Communication.Runtime.Protocols.Mitsubishi;
using MachineApp.Infra.Plc;
using MachineApp.Infra.Tool;
using Prism.Ioc;
using Xunit;

namespace MachineApp.Host.Tests;

[Collection("ProductionComposition")]
public sealed class PlcHeartbeatLifecycleTests(ProductionFixture fixture)
{
    [Fact]
    public async Task LateHeartbeatAfterStopAndDisconnectDoesNotReconnect()
    {
        var adapter = new ScriptedAdapter();
        await using var service = CreateService(adapter);
        service.StartHeartbeat();
        service.StopHeartbeat();
        await service.DisconnectAsync();
        await TickAsync(service);
        Assert.Equal(0, adapter.ConnectCalls);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task HeartbeatAlreadyWaitingAtGateRechecksStopBeforeConnecting()
    {
        var adapter = new ScriptedAdapter();
        await using var service = CreateService(adapter);
        var gate = Gate(service);
        await gate.WaitAsync();
        Task? tick = null;
        Task? disconnect = null;
        try
        {
            service.StartHeartbeat();
            tick = TickAsync(service);
            Assert.False(tick.IsCompleted);
            service.StopHeartbeat();
            disconnect = service.DisconnectAsync();
            Assert.False(disconnect.IsCompleted);
        }
        finally
        {
            gate.Release();
            if (tick != null) await tick.WaitAsync(TimeSpan.FromSeconds(10));
            if (disconnect != null) await disconnect.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(0, adapter.ConnectCalls);
        Assert.Equal(ConnectionState.Disconnected, adapter.State);
    }

    [Fact]
    public async Task DisconnectWaitsForEnteredHeartbeatConnectionThenLeavesOffline()
    {
        var adapter = new ScriptedAdapter { HoldConnect = true };
        await using var service = CreateService(adapter);
        service.StartHeartbeat();
        var tick = TickAsync(service);
        await adapter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        service.StopHeartbeat();
        var disconnect = service.DisconnectAsync();
        try { Assert.False(disconnect.IsCompleted); }
        finally
        {
            adapter.Release.TrySetResult();
            await tick.WaitAsync(TimeSpan.FromSeconds(10));
            await disconnect.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(1, adapter.ConnectCalls);
        Assert.Equal(ConnectionState.Disconnected, adapter.State);
        Assert.False(service.IsConnected);
        await TickAsync(service);
        Assert.Equal(1, adapter.ConnectCalls);
    }

    [Fact]
    public async Task RestartAndExplicitManualConnectionRemainAvailable()
    {
        var adapter = new ScriptedAdapter();
        await using var service = CreateService(adapter);
        service.StartHeartbeat();
        await TickAsync(service);
        Assert.True(service.IsConnected);
        service.StopHeartbeat();
        await service.DisconnectAsync();
        service.StartHeartbeat();
        await TickAsync(service);
        Assert.True(service.IsConnected);
        Assert.Equal(2, adapter.ConnectCalls);
        service.StopHeartbeat();
        await service.DisconnectAsync();
        Assert.True(await service.ConnectAsync());
        Assert.Equal(3, adapter.ConnectCalls);
    }

    [Fact]
    public async Task InitialProductionRefreshQueuedAtGateAlsoHonorsStop()
    {
        var adapter = new ScriptedAdapter();
        await using var service = CreateService(adapter);
        var gate = Gate(service);
        await gate.WaitAsync();
        Task? stop = null;
        try
        {
            fixture.Invoke(container =>
            {
                // This provider deliberately rejects logging so no persistence backend is opened.
                var provider = DispatchProxy.Create<IContainerProvider, HostWindowShutdownTests.ContainerProxy>();
                var plc = new PLCTool(provider, service, container.Resolve<PlcTagCatalog>());
                plc.Refresh();
                stop = plc.StopRefreshAsync();
                Assert.False(stop.IsCompleted);
            });
        }
        finally
        {
            gate.Release();
            if (stop != null) await stop.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(0, adapter.ConnectCalls);
        Assert.False(service.IsConnected);
    }

    private static PlcService CreateService(ScriptedAdapter adapter)
    {
        // Keep the timer inert during deterministic callback execution; construct but never open SLMP.
        var options = new PlcOptions { Backend = "Slmp", HeartbeatIntervalMs = 60000 };
        var service = new PlcService(options);
        var field = typeof(PlcService).GetField("_device", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((McClientDevice)field.GetValue(service)!).Dispose();
        field.SetValue(service, new McClientDevice(options.ToMcParam(), adapter));
        return service;
    }

    private static SemaphoreSlim Gate(PlcService service) =>
        (SemaphoreSlim)typeof(PlcService).GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;

    private static Task TickAsync(PlcService service) =>
        (Task)typeof(PlcService).GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, null)!;

    private sealed class ScriptedAdapter : IMcClientAdapter
    {
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public int ConnectCalls { get; private set; }
        public bool HoldConnect { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            Entered.TrySetResult();
            if (HoldConnect) await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            State = ConnectionState.Connected;
        }
        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            State = ConnectionState.Disconnected;
            return ValueTask.CompletedTask;
        }
        public ValueTask<McResponse> ExecuteAsync(McRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Heartbeat lifecycle tests must not send PLC requests.");
        public ValueTask DisposeAsync() { State = ConnectionState.Disconnected; return ValueTask.CompletedTask; }
    }
}
