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
}
