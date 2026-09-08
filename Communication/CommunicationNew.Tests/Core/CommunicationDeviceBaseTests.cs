using CommunicationNew.Core;
using Xunit;

namespace CommunicationNew.Tests.Core;

public sealed class CommunicationDeviceBaseTests
{
    [Fact]
    public async Task Concurrent_dispose_calls_share_completion_and_cleanup_once()
    {
        var device = new BlockingDisposeDevice();

        var firstDispose = device.DisposeAsync().AsTask();
        await device.DisconnectStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var secondDispose = device.DisposeAsync().AsTask();

        try
        {
            Assert.Same(firstDispose, secondDispose);
        }
        finally
        {
            device.ResumeDisconnect.TrySetResult();
        }

        await Task.WhenAll(firstDispose, secondDispose);
        Assert.Equal(1, device.DisconnectCount);
        Assert.Equal(1, device.CleanupCount);
    }

    private sealed class BlockingDisposeDevice : CommunicationDeviceBase
    {
        public BlockingDisposeDevice()
            : base(new TestParam())
        {
        }

        public TaskCompletionSource DisconnectStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ResumeDisconnect { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DisconnectCount { get; private set; }

        public int CleanupCount { get; private set; }

        public override DeviceRole Role => DeviceRole.Client;

        public override Task<bool> ConnectAsync(CancellationToken ct = default) =>
            Task.FromResult(true);

        public override async Task DisconnectAsync(CancellationToken ct = default)
        {
            DisconnectCount++;
            DisconnectStarted.TrySetResult();
            await ResumeDisconnect.Task.WaitAsync(ct);
        }

        public override Task<byte[]> ReadAsync(
            string address,
            int length,
            CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());

        public override Task<bool> WriteAsync(
            string address,
            byte[] data,
            CancellationToken ct = default) =>
            Task.FromResult(true);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CleanupCount++;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class TestParam : CommunicationParam
    {
    }
}
