using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using CommunicationNew.Core.EventArgs;
using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;
using CommunicationNew.Core.Services;
using Xunit;

namespace CommunicationNew.Tests.Runtime;

public sealed class DataPointAutoReaderTests
{
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

        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

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

        public Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
            Task.FromResult(true);
        public Task<bool> SendAsync(byte[] data, CancellationToken ct = default) => Task.FromResult(true);
        public Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

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

        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            var value = Interlocked.Increment(ref _readCount);
            if (value >= 3)
            {
                ThirdRead.TrySetResult();
            }

            return Task.FromResult(new[] { (byte)(value >> 8), (byte)value });
        }

        public Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
            Task.FromResult(true);
        public Task<bool> SendAsync(byte[] data, CancellationToken ct = default) => Task.FromResult(true);
        public Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

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

        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
            Task.FromException<byte[]>(error);
        public Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
            Task.FromResult(true);
        public Task<bool> SendAsync(byte[] data, CancellationToken ct = default) => Task.FromResult(true);
        public Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

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

        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
            throw new InvalidOperationException("支持批量时不应逐点读取。");
        public Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
            Task.FromResult(true);
        public Task<bool> SendAsync(byte[] data, CancellationToken ct = default) => Task.FromResult(true);
        public Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }

    private sealed class AddressReaderOnlyAdapter : IAddressReader
    {
        public Task<byte[]> ReadAsync(
            string address,
            int length,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new byte[] { 0, 42 });
    }

    private sealed class ReaderOnlyAdapter : IDataPointBatchReader
    {
        public Task<IReadOnlyList<DataPointReadResult>> ReadPointsAsync(
            IReadOnlyList<IDataPoint> points,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DataPointReadResult>>(
                new[] { DataPointReadResult.Success(points[0], new byte[] { 0x00, 0x2A }) });
    }
}
