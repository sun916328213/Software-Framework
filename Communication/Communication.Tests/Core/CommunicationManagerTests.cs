using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using CommunicationNew.Core.EventArgs;
using Xunit;

namespace CommunicationNew.Tests.Core;

public sealed class CommunicationManagerTests
{
    [Fact]
    public async Task Duplicate_device_id_is_rejected_without_replacing_owned_device()
    {
        var first = new TrackingDevice("shared-id");
        var duplicate = new TrackingDevice("shared-id");
        var factory = new SequenceFactory(first, duplicate);
        var manager = new CommunicationManager(factory);

        try
        {
            var registered = manager.CreateDevice(new TestParam());

            var error = Assert.Throws<InvalidOperationException>(
                () => manager.CreateDevice(new TestParam()));

            Assert.Contains("shared-id", error.Message);
            Assert.Same(registered, manager.GetDevice("shared-id"));
            Assert.True(duplicate.IsDisposed);
            Assert.False(first.IsDisposed);
        }
        finally
        {
            await manager.DisposeAsync();
        }
    }

    [Fact]
    public async Task Duplicate_external_device_is_rejected_without_taking_ownership()
    {
        var first = new TrackingDevice("shared-id");
        var duplicate = new TrackingDevice("shared-id");
        var manager = new CommunicationManager(new SequenceFactory());

        try
        {
            manager.RegisterDevice(first);

            var error = Assert.Throws<InvalidOperationException>(
                () => manager.RegisterDevice(duplicate));

            Assert.Contains("shared-id", error.Message);
            Assert.Same(first, manager.GetDevice("shared-id"));
            Assert.False(duplicate.IsDisposed);
        }
        finally
        {
            await manager.DisposeAsync();
            duplicate.Dispose();
        }
    }

    [Fact]
    public async Task Disposed_manager_rejects_new_devices_before_taking_ownership()
    {
        var factoryDevice = new TrackingDevice("factory-device");
        var externalDevice = new TrackingDevice("external-device");
        var factory = new SequenceFactory(factoryDevice);
        var manager = new CommunicationManager(factory);
        await manager.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(
            () => manager.CreateDevice(new TestParam()));
        Assert.Throws<ObjectDisposedException>(
            () => manager.RegisterDevice(externalDevice));
        Assert.Equal(0, factory.CreateCount);
        Assert.False(factoryDevice.IsDisposed);
        Assert.False(externalDevice.IsDisposed);

        factoryDevice.Dispose();
        externalDevice.Dispose();
    }

    [Fact]
    public async Task Disposed_manager_rejects_device_removal()
    {
        var manager = new CommunicationManager(new SequenceFactory());
        await manager.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => manager.RemoveDeviceAsync("missing-device"));
    }

    [Fact]
    public async Task Concurrent_dispose_calls_wait_for_the_same_device_cleanup()
    {
        var device = new TrackingDevice(
            "slow-device",
            blockDisconnect: true,
            disposeDisconnects: true);
        var manager = new CommunicationManager(new SequenceFactory());
        manager.RegisterDevice(device);

        var firstDispose = manager.DisposeAsync().AsTask();
        await device.DisconnectEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var concurrentDispose = manager.DisposeAsync().AsTask();

        Assert.False(concurrentDispose.IsCompleted);
        device.AllowDisconnect.SetResult();
        await Task.WhenAll(firstDispose, concurrentDispose);

        Assert.Equal(1, device.DisconnectCount);
        Assert.Equal(1, device.DisposeCount);
    }

    [Fact]
    public async Task Manager_disposal_does_not_disconnect_device_twice()
    {
        var device = new TrackingDevice("owned-device", disposeDisconnects: true);
        var manager = new CommunicationManager(new SequenceFactory());
        manager.RegisterDevice(device);

        await manager.DisposeAsync();

        Assert.Equal(1, device.DisconnectCount);
        Assert.Equal(1, device.DisposeCount);
    }

    [Fact]
    public async Task Removing_device_runs_its_shutdown_chain_once()
    {
        var device = new TrackingDevice("removed-device", disposeDisconnects: true);
        await using var manager = new CommunicationManager(new SequenceFactory());
        manager.RegisterDevice(device);

        var removed = await manager.RemoveDeviceAsync(device.DeviceId);

        Assert.True(removed);
        Assert.Null(manager.GetDevice(device.DeviceId));
        Assert.Equal(1, device.DisconnectCount);
        Assert.Equal(1, device.DisposeCount);
    }

    [Fact]
    public async Task Manager_disposal_waits_for_device_removal_already_in_progress()
    {
        var device = new TrackingDevice(
            "removing-device",
            blockDisconnect: true,
            disposeDisconnects: true);
        var manager = new CommunicationManager(new SequenceFactory());
        manager.RegisterDevice(device);

        var removal = manager.RemoveDeviceAsync(device.DeviceId);
        await device.DisconnectEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var managerDisposal = manager.DisposeAsync().AsTask();

        Assert.False(managerDisposal.IsCompleted);
        device.AllowDisconnect.SetResult();
        Assert.True(await removal);
        await managerDisposal;
        Assert.Equal(1, device.DisposeCount);
    }

    [Fact]
    public async Task Connect_all_reports_legacy_device_false_result_as_failure()
    {
        var device = new TrackingDevice("offline-device", connectResult: false);
        await using var manager = new CommunicationManager(new SequenceFactory());
        manager.RegisterDevice(device);

        var error = await Assert.ThrowsAsync<IOException>(
            () => manager.ConnectAllAsync());

        Assert.Contains("offline-device", error.Message);
        Assert.Equal(1, device.ConnectCount);
    }

    [Fact]
    public async Task Connect_all_preserves_every_device_failure()
    {
        var first = new TrackingDevice("first-offline-device", connectResult: false);
        var second = new TrackingDevice("second-offline-device", connectResult: false);
        await using var manager = new CommunicationManager(new SequenceFactory());
        manager.RegisterDevice(first);
        manager.RegisterDevice(second);

        var error = await Assert.ThrowsAsync<AggregateException>(
            () => manager.ConnectAllAsync());
        var messages = error.Flatten().InnerExceptions.Select(item => item.Message).ToArray();

        Assert.Contains(messages, message => message.Contains("first-offline-device"));
        Assert.Contains(messages, message => message.Contains("second-offline-device"));
        Assert.Equal(1, first.ConnectCount);
        Assert.Equal(1, second.ConnectCount);
    }

    [Fact]
    public async Task Batch_lifecycle_prefers_the_new_connection_capability()
    {
        var device = new ConnectionTrackingDevice("connection-device");
        await using var manager = new CommunicationManager(new SequenceFactory());
        manager.RegisterDevice(device);

        await manager.ConnectAllAsync();
        await manager.DisconnectAllAsync();

        Assert.Equal(1, device.CapabilityConnectCount);
        Assert.Equal(1, device.CapabilityDisconnectCount);
        Assert.Equal(0, device.ConnectCount);
        Assert.Equal(0, device.DisconnectCount);
    }

    [Fact]
    public async Task Manager_disposal_waits_for_batch_connection_already_in_progress()
    {
        var device = new ConnectionTrackingDevice(
            "connecting-device",
            blockConnect: true);
        var manager = new CommunicationManager(new SequenceFactory());
        manager.RegisterDevice(device);

        var connection = manager.ConnectAllAsync();
        await device.ConnectEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var managerDisposal = manager.DisposeAsync().AsTask();

        Assert.False(managerDisposal.IsCompleted);
        device.AllowConnect.SetResult();
        await connection;
        await managerDisposal;
        Assert.Equal(1, device.DisposeCount);
    }

    [Fact]
    public async Task Device_removal_waits_for_batch_connection_already_in_progress()
    {
        var device = new ConnectionTrackingDevice(
            "connected-then-removed-device",
            blockConnect: true);
        var manager = new CommunicationManager(new SequenceFactory());
        Task? connection = null;
        try
        {
            manager.RegisterDevice(device);
            connection = manager.ConnectAllAsync();
            await device.ConnectEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
            var removal = manager.RemoveDeviceAsync(device.DeviceId);

            Assert.False(removal.IsCompleted);
            Assert.Equal(0, device.DisposeCount);
            device.AllowConnect.SetResult();
            await connection;
            Assert.True(await removal);
            Assert.Equal(1, device.DisposeCount);
        }
        finally
        {
            // 即使红灯断言提前退出，也必须解除测试设备阻塞，避免测试清理悬挂。
            device.AllowConnect.TrySetResult();
            if (connection is not null)
            {
                await connection;
            }
            await manager.DisposeAsync();
        }
    }

    [Fact]
    public async Task Batch_lifecycle_operations_run_in_submission_order()
    {
        var device = new ConnectionTrackingDevice(
            "ordered-lifecycle-device",
            blockConnect: true);
        var manager = new CommunicationManager(new SequenceFactory());
        Task? connection = null;
        Task? disconnection = null;
        try
        {
            manager.RegisterDevice(device);
            connection = manager.ConnectAllAsync();
            await device.ConnectEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
            disconnection = manager.DisconnectAllAsync();

            Assert.False(disconnection.IsCompleted);
            Assert.False(device.CapabilityDisconnectEntered.Task.IsCompleted);
            device.AllowConnect.SetResult();
            await Task.WhenAll(connection, disconnection);
            Assert.Equal(1, device.CapabilityConnectCount);
            Assert.Equal(1, device.CapabilityDisconnectCount);
        }
        finally
        {
            device.AllowConnect.TrySetResult();
            if (connection is not null)
            {
                await connection;
            }
            if (disconnection is not null)
            {
                await disconnection;
            }
            await manager.DisposeAsync();
        }
    }

    [Fact]
    public async Task Disconnect_all_invokes_every_device_when_adapters_throw_synchronously()
    {
        var first = new ConnectionTrackingDevice(
            "first-disconnect-failure",
            throwOnDisconnect: true);
        var second = new ConnectionTrackingDevice(
            "second-disconnect-failure",
            throwOnDisconnect: true);
        await using var manager = new CommunicationManager(new SequenceFactory());
        manager.RegisterDevice(first);
        manager.RegisterDevice(second);

        var error = await Assert.ThrowsAsync<AggregateException>(
            () => manager.DisconnectAllAsync());
        var messages = error.Flatten().InnerExceptions.Select(item => item.Message).ToArray();

        Assert.Contains(messages, message => message.Contains("first-disconnect-failure"));
        Assert.Contains(messages, message => message.Contains("second-disconnect-failure"));
        Assert.Equal(1, first.CapabilityDisconnectCount);
        Assert.Equal(1, second.CapabilityDisconnectCount);
    }

    [Fact]
    public async Task Disconnect_all_propagates_cancellation_to_connection_capability()
    {
        var device = new ConnectionTrackingDevice(
            "cancel-disconnect-device",
            blockDisconnect: true);
        await using var manager = new CommunicationManager(new SequenceFactory());
        manager.RegisterDevice(device);
        using var cancellation = new CancellationTokenSource();

        var disconnection = manager.DisconnectAllAsync(cancellation.Token);
        await device.CapabilityDisconnectEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => disconnection);
        Assert.True(disconnection.IsCanceled);
    }

    private sealed class SequenceFactory(params ICommunicationDevice[] devices)
        : ICommunicationDeviceFactory
    {
        private readonly Queue<ICommunicationDevice> _devices = new(devices);
        public int CreateCount { get; private set; }

        public ICommunicationDevice Create(CommunicationParam parameter)
        {
            CreateCount++;
            return _devices.Dequeue();
        }
    }

    private sealed class TestParam : CommunicationParam
    {
    }

    private class TrackingDevice(
        string deviceId,
        bool blockDisconnect = false,
        bool disposeDisconnects = false,
        bool connectResult = true)
        : ICommunicationDevice
    {
        public TaskCompletionSource DisconnectEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowDisconnect { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DisconnectCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int ConnectCount { get; private set; }
        public bool IsDisposed => DisposeCount > 0;
        public string DeviceName { get; set; } = deviceId;
        public string DeviceId { get; } = deviceId;
        public ProtocolType ProtocolType => ProtocolType.Tcp;
        public DeviceRole Role => DeviceRole.Client;
        public bool IsConnected => false;
        public int TimeoutMs { get; set; } = 1000;

        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<DataReceivedEventArgs>? DataReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<CommunicationErrorEventArgs>? ErrorOccurred
        {
            add { }
            remove { }
        }

        public Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            ConnectCount++;
            return Task.FromResult(connectResult);
        }
        public async Task DisconnectAsync(CancellationToken ct = default)
        {
            DisconnectCount++;
            DisconnectEntered.TrySetResult();
            if (blockDisconnect)
            {
                await AllowDisconnect.Task.WaitAsync(ct);
            }
        }
        public Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());
        public Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
            Task.FromResult(true);
        public Task<bool> SendAsync(byte[] data, CancellationToken ct = default) => Task.FromResult(true);
        public Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());

        public void Dispose() => DisposeCount++;

        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            if (disposeDisconnects)
            {
                await DisconnectAsync(CancellationToken.None);
            }
        }
    }

    private sealed class ConnectionTrackingDevice(
        string deviceId,
        bool blockConnect = false,
        bool blockDisconnect = false,
        bool throwOnDisconnect = false)
        : TrackingDevice(deviceId), IConnection
    {
        public TaskCompletionSource ConnectEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowConnect { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CapabilityDisconnectEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowCapabilityDisconnect { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CapabilityConnectCount { get; private set; }
        public int CapabilityDisconnectCount { get; private set; }
        public ConnectionState State => ConnectionState.Disconnected;

        async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
        {
            CapabilityConnectCount++;
            ConnectEntered.TrySetResult();
            if (blockConnect)
            {
                await AllowConnect.Task.WaitAsync(cancellationToken);
            }
        }

        ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken)
        {
            CapabilityDisconnectCount++;
            CapabilityDisconnectEntered.TrySetResult();
            if (throwOnDisconnect)
            {
                throw new IOException($"设备 '{DeviceId}' 断开失败。");
            }

            return blockDisconnect
                ? new ValueTask(AllowCapabilityDisconnect.Task.WaitAsync(cancellationToken))
                : ValueTask.CompletedTask;
        }
    }
}
