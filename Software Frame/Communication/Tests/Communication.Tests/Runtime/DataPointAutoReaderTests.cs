// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DataPointAutoReaderTests、BlockingDevice、CountingDevice、FailingDevice、BatchDevice、FixedTimeProvider、AddressReaderOnlyAdapter、ReaderOnlyAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Abstractions、Communication.Core、Communication.Runtime.Core、Communication.Core.EventArgs、Communication.Core.Interfaces、Communication.Core.Models。导入不代表所有类型都会在运行时加载。
using Communication.Core.Abstractions;
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Core.EventArgs;
using Communication.Core.Interfaces;
using Communication.Core.Models;
using Communication.Core.Services;
using Communication.Runtime.Core.Services;
using Xunit;

namespace Communication.Tests.Runtime;

/// <summary>
/// DataPointAutoReaderTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class DataPointAutoReaderTests
{
    /// <summary>
    /// 验证 Address_reader_capability_can_drive_polling_without_legacy_device_interface 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、PollOnceAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Address_reader_capability_can_drive_polling_without_legacy_device_interface()
    {
        var addressReader = new AddressReaderOnlyAdapter();
        var points = new DataPointCollection();
        var point = new DataPoint("Temperature", DataPointType.Int16, "40001");
        points.Add(point);
        await using var reader = new DataPointAutoReader(
            addressReader,
            points,
            "test-protocol");

        await reader.PollOnceAsync();

        Assert.Equal((short)42, point.Current.Value);
    }

    /// <summary>
    /// 验证 Reader_only_adapter_can_drive_polling_without_legacy_device_interface 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、PollOnceAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Reader_only_adapter_can_drive_polling_without_legacy_device_interface()
    {
        var pointReader = new ReaderOnlyAdapter();
        var points = new DataPointCollection();
        var point = new DataPoint("Temperature", DataPointType.Int16, "40001");
        points.Add(point);
        await using var reader = new DataPointAutoReader(
            pointReader,
            points,
            "test-protocol",
            TimeProvider.System);

        await reader.PollOnceAsync();

        Assert.Equal((short)42, point.Current.Value);
    }

    /// <summary>
    /// 验证 Batch_capable_device_reads_adjacent_points_through_one_operation 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 AddRange、PollOnceAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Batch_capable_device_reads_adjacent_points_through_one_operation()
    {
        await using var device = new BatchDevice();
        var points = new DataPointCollection();
        var first = new DataPoint("First", DataPointType.Int16, "HR1");
        var second = new DataPoint("Second", DataPointType.Int16, "HR2");
        points.AddRange(new IDataPoint[] { first, second });
        await using var reader = new DataPointAutoReader(device, points);

        await reader.PollOnceAsync();

        Assert.Equal(1, device.BatchReadCount);
        Assert.Equal((short)0x1234, first.Current.Value);
        Assert.Equal((short)0x5678, second.Current.Value);
    }

    /// <summary>
    /// 验证 Failed_read_publishes_a_timestamped_bad_quality_update 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、PollOnceAsync、Equal、Same；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Failed_read_publishes_a_timestamped_bad_quality_update()
    {
        var error = new TimeoutException("设备读取超时");
        var timestamp = new DateTimeOffset(2026, 9, 4, 9, 0, 0, TimeSpan.Zero);
        await using var device = new FailingDevice(error);
        var points = new DataPointCollection();
        var point = new DataPoint("Temperature", DataPointType.Int16, "40001");
        points.Add(point);
        await using var reader = new DataPointAutoReader(
            device,
            points,
            new FixedTimeProvider(timestamp));

        await reader.PollOnceAsync();

        Assert.Equal(DataQuality.Bad, point.Current.Quality);
        Assert.Equal(timestamp, point.Current.Timestamp);
        Assert.Same(error, point.Current.Error);
    }

    /// <summary>
    /// 验证 Stop_async_waits_until_the_active_read_observes_cancellation 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、Start、WaitAsync、FromSeconds、StopAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Stop_async_waits_until_the_active_read_observes_cancellation()
    {
        await using var device = new BlockingDevice();
        var points = new DataPointCollection();
        points.Add(new DataPoint("Temperature", DataPointType.Int16, "40001"));
        await using var reader = new DataPointAutoReader(device, points)
        {
            IntervalMs = 10,
        };

        reader.Start();
        await device.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await reader.StopAsync();

        Assert.True(device.ReadExited.Task.IsCompletedSuccessfully);
        Assert.False(reader.IsRunning);
    }

    /// <summary>
    /// 验证 Disposed_reader_rejects_new_background_and_manual_polls 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 DisposeAsync、Throws、Start、ThrowsAsync、PollOnceAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Disposed_reader_rejects_new_background_and_manual_polls()
    {
        var reader = new DataPointAutoReader(
            new ReaderOnlyAdapter(),
            new DataPointCollection(),
            "test-protocol",
            TimeProvider.System);

        await reader.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => reader.Start());
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => reader.PollOnceAsync());
        Assert.False(reader.IsRunning);
    }

    /// <summary>
    /// 验证 Slow_change_subscriber_does_not_block_background_scanning 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、TrySetResult、GetResult、GetAwaiter、Start；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Slow_change_subscriber_does_not_block_background_scanning()
    {
        await using var device = new CountingDevice();
        var points = new DataPointCollection();
        points.Add(new DataPoint("Temperature", DataPointType.Int16, "40001"));
        await using var reader = new DataPointAutoReader(device, points)
        {
            // Windows 定时器对亚毫秒剩余时间可能立即完成；使用稳定间隔避免测试忙轮询。
            IntervalMs = 20,
        };
        var subscriberEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSubscriber = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        reader.DataPointChanged += (_, _) =>
        {
            subscriberEntered.TrySetResult();
            releaseSubscriber.Task.GetAwaiter().GetResult();
        };

        reader.Start();
        await subscriberEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await device.ThirdRead.Task.WaitAsync(TimeSpan.FromSeconds(1));

        releaseSubscriber.SetResult();
        await reader.StopAsync();
        Assert.True(device.ReadCount >= 3);
    }

    /// <summary>
    /// BlockingDevice 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ICommunicationDevice。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class BlockingDevice : ICommunicationDevice
    {
        public TaskCompletionSource ReadEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReadExited { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string DeviceName { get; set; } = "Blocking";
        public string DeviceId { get; } = "blocking";
        public ProtocolType ProtocolType => ProtocolType.ModbusTcp;
        public DeviceRole Role => DeviceRole.Client;
        public bool IsConnected => true;
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
        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(true);
        /// <summary>
        /// 验证 DisconnectAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

        /// <summary>
        /// 验证 ReadAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 TrySetResult、Delay、Empty；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task<byte[]> ReadAsync(
            string address,
            int length,
            CancellationToken ct = default)
        {
            ReadEntered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return Array.Empty<byte>();
            }
            finally
            {
                ReadExited.TrySetResult();
            }
        }

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
        public void Dispose()
        {
        }

        /// <summary>
        /// 验证 DisposeAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// CountingDevice 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ICommunicationDevice。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class CountingDevice : ICommunicationDevice
    {
        private int _readCount;

        public TaskCompletionSource ThirdRead { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ReadCount => Volatile.Read(ref _readCount);
        public string DeviceName { get; set; } = "Counting";
        public string DeviceId { get; } = "counting";
        public ProtocolType ProtocolType => ProtocolType.ModbusTcp;
        public DeviceRole Role => DeviceRole.Client;
        public bool IsConnected => true;
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
        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(true);
        /// <summary>
        /// 验证 DisconnectAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

        /// <summary>
        /// 验证 ReadAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Increment、TrySetResult、FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            var value = Interlocked.Increment(ref _readCount);
            if (value >= 3)
            {
                ThirdRead.TrySetResult();
            }

            return Task.FromResult(new[] { (byte)(value >> 8), (byte)value });
        }

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
        public void Dispose() { }
        /// <summary>
        /// 验证 DisposeAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// FailingDevice 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ICommunicationDevice。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class FailingDevice(Exception error) : ICommunicationDevice
    {
        public string DeviceName { get; set; } = "Failing";
        public string DeviceId { get; } = "failing";
        public ProtocolType ProtocolType => ProtocolType.ModbusTcp;
        public DeviceRole Role => DeviceRole.Client;
        public bool IsConnected => true;
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
        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(true);
        /// <summary>
        /// 验证 DisconnectAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        /// <summary>
        /// 验证 ReadAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromException；具体条件和先后次序见方法体。
        /// </summary>
        public Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
            Task.FromException<byte[]>(error);
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
        public void Dispose() { }
        /// <summary>
        /// 验证 DisposeAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// BatchDevice 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ICommunicationDevice、IDataPointBatchReader。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class BatchDevice : ICommunicationDevice, IDataPointBatchReader
    {
        public int BatchReadCount { get; private set; }
        public string DeviceName { get; set; } = "Batch";
        public string DeviceId { get; } = "batch";
        public ProtocolType ProtocolType => ProtocolType.ModbusTcp;
        public DeviceRole Role => DeviceRole.Client;
        public bool IsConnected => true;
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
        /// 验证 ReadPointsAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult、Success；具体条件和先后次序见方法体。
        /// </summary>
        public Task<IReadOnlyList<DataPointReadResult>> ReadPointsAsync(
            IReadOnlyList<IDataPoint> points,
            CancellationToken cancellationToken = default)
        {
            BatchReadCount++;
            return Task.FromResult<IReadOnlyList<DataPointReadResult>>(new[]
            {
                DataPointReadResult.Success(points[0], new byte[] { 0x12, 0x34 }),
                DataPointReadResult.Success(points[1], new byte[] { 0x56, 0x78 }),
            });
        }

        /// <summary>
        /// 验证 ConnectAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(true);
        /// <summary>
        /// 验证 DisconnectAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        /// <summary>
        /// 验证 ReadAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
            throw new InvalidOperationException("支持批量时不应逐点读取。");
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
        public void Dispose() { }
        /// <summary>
        /// 验证 DisposeAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// FixedTimeProvider 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：TimeProvider。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        /// <summary>
        /// 验证 GetUtcNow 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override DateTimeOffset GetUtcNow() => timestamp;
    }

    /// <summary>
    /// AddressReaderOnlyAdapter 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IAddressReader。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class AddressReaderOnlyAdapter : IAddressReader
    {
        /// <summary>
        /// 验证 ReadAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public Task<byte[]> ReadAsync(
            string address,
            int length,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new byte[] { 0, 42 });
    }

    /// <summary>
    /// ReaderOnlyAdapter 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IDataPointBatchReader。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ReaderOnlyAdapter : IDataPointBatchReader
    {
        /// <summary>
        /// 验证 ReadPointsAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult、Success；具体条件和先后次序见方法体。
        /// </summary>
        public Task<IReadOnlyList<DataPointReadResult>> ReadPointsAsync(
            IReadOnlyList<IDataPoint> points,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DataPointReadResult>>(
                new[] { DataPointReadResult.Success(points[0], new byte[] { 0x00, 0x2A }) });
    }
}
