// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CommunicationManagerTests、SequenceFactory、TestParam、TrackingDevice、ConnectionTrackingDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Abstractions、Communication.Core、Communication.Runtime.Core、Communication.Core.EventArgs、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Abstractions;
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Core.EventArgs;
using Xunit;

namespace Communication.Tests.Core;

/// <summary>
/// CommunicationManagerTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class CommunicationManagerTests
{
    /// <summary>
    /// 验证 Duplicate_device_id_is_rejected_without_replacing_owned_device 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateDevice、Throws、Contains、Same、GetDevice；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Duplicate_external_device_is_rejected_without_taking_ownership 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterDevice、Throws、Contains、Same、GetDevice；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Disposed_manager_rejects_new_devices_before_taking_ownership 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 DisposeAsync、Throws、CreateDevice、RegisterDevice、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Disposed_manager_rejects_device_removal 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 DisposeAsync、ThrowsAsync、RemoveDeviceAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Disposed_manager_rejects_device_removal()
    {
        var manager = new CommunicationManager(new SequenceFactory());
        await manager.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => manager.RemoveDeviceAsync("missing-device"));
    }

    /// <summary>
    /// 验证 Concurrent_dispose_calls_wait_for_the_same_device_cleanup 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterDevice、AsTask、DisposeAsync、WaitAsync、FromSeconds；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Manager_disposal_does_not_disconnect_device_twice 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterDevice、DisposeAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Removing_device_runs_its_shutdown_chain_once 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterDevice、RemoveDeviceAsync、True、Null、GetDevice；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Manager_disposal_waits_for_device_removal_already_in_progress 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterDevice、RemoveDeviceAsync、WaitAsync、FromSeconds、AsTask；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Connect_all_reports_legacy_device_false_result_as_failure 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterDevice、ThrowsAsync、ConnectAllAsync、Contains、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Connect_all_preserves_every_device_failure 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterDevice、ThrowsAsync、ConnectAllAsync、ToArray、Select；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Batch_lifecycle_prefers_the_new_connection_capability 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterDevice、ConnectAllAsync、DisconnectAllAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Manager_disposal_waits_for_batch_connection_already_in_progress 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterDevice、ConnectAllAsync、WaitAsync、FromSeconds、AsTask；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Device_removal_waits_for_batch_connection_already_in_progress 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterDevice、ConnectAllAsync、WaitAsync、FromSeconds、RemoveDeviceAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Batch_lifecycle_operations_run_in_submission_order 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterDevice、ConnectAllAsync、WaitAsync、FromSeconds、DisconnectAllAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Disconnect_all_invokes_every_device_when_adapters_throw_synchronously 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterDevice、ThrowsAsync、DisconnectAllAsync、ToArray、Select；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Disconnect_all_propagates_cancellation_to_connection_capability 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterDevice、DisconnectAllAsync、WaitAsync、FromSeconds、Cancel；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// SequenceFactory 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ICommunicationDeviceFactory。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class SequenceFactory(params ICommunicationDevice[] devices)
        : ICommunicationDeviceFactory
    {
        private readonly Queue<ICommunicationDevice> _devices = new(devices);
        public int CreateCount { get; private set; }

        /// <summary>
        /// 验证 Create 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Dequeue；具体条件和先后次序见方法体。
        /// </summary>
        public ICommunicationDevice Create(CommunicationParam parameter)
        {
            CreateCount++;
            return _devices.Dequeue();
        }
    }

    /// <summary>
    /// TestParam 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：CommunicationParam。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class TestParam : CommunicationParam
    {
    }

    /// <summary>
    /// TrackingDevice 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ICommunicationDevice。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

        /// <summary>
        /// 验证 ConnectAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            ConnectCount++;
            return Task.FromResult(connectResult);
        }
        /// <summary>
        /// 验证 DisconnectAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 TrySetResult、WaitAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task DisconnectAsync(CancellationToken ct = default)
        {
            DisconnectCount++;
            DisconnectEntered.TrySetResult();
            if (blockDisconnect)
            {
                await AllowDisconnect.Task.WaitAsync(ct);
            }
        }
        /// <summary>
        /// 验证 ReadAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult、Empty；具体条件和先后次序见方法体。
        /// </summary>
        public Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());
        /// <summary>
        /// 验证 WriteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
            Task.FromResult(true);
        /// <summary>
        /// 验证 SendAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public Task<bool> SendAsync(byte[] data, CancellationToken ct = default) => Task.FromResult(true);
        /// <summary>
        /// 验证 SendAndReceiveAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult、Empty；具体条件和先后次序见方法体。
        /// </summary>
        public Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());

        /// <summary>
        /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public void Dispose() => DisposeCount++;

        /// <summary>
        /// 验证 DisposeAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 DisconnectAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            if (disposeDisconnects)
            {
                await DisconnectAsync(CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// ConnectionTrackingDevice 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：TrackingDevice、IConnection。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

        /// <summary>
        /// 验证 ConnectAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 TrySetResult、WaitAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
        {
            CapabilityConnectCount++;
            ConnectEntered.TrySetResult();
            if (blockConnect)
            {
                await AllowConnect.Task.WaitAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 验证 DisconnectAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 TrySetResult、WaitAsync；具体条件和先后次序见方法体。
        /// </summary>
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
