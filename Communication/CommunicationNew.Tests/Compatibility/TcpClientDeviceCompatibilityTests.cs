using System.Net;
using System.Net.Sockets;
using System.Diagnostics.Metrics;
using CommunicationNew.Abstractions;
using CommunicationNew.Protocols.Socket;
using CommunicationNew.Runtime;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

[Collection("Diagnostics")]
public sealed class TcpClientDeviceCompatibilityTests
{
    [Fact]
    public async Task Legacy_tcp_client_uses_connection_seam_and_one_receive_owner()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var device = new TcpClientDevice(new SocketParam
            {
                ServerIP = IPAddress.Loopback.ToString(),
                Port = port,
                Delimiter = null,
                TimeoutMs = 1000,
            });
            var received = new TaskCompletionSource<byte[]>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            device.DataReceived += (_, args) => received.TrySetResult(args.Data);

            var acceptTask = listener.AcceptTcpClientAsync();
            Assert.IsAssignableFrom<IConnection>(device);
            Assert.True(await device.ConnectAsync());
            using var peer = await acceptTask;
            await using var stream = peer.GetStream();

            await stream.WriteAsync(new byte[] { 0x41, 0x42 });
            Assert.Equal(
                new byte[] { 0x41, 0x42 },
                await received.Task.WaitAsync(TimeSpan.FromSeconds(1)));

            var responseTask = device.SendAndReceiveAsync(new byte[] { 0x10 });
            var request = new byte[1];
            await stream.ReadExactlyAsync(request);
            Assert.Equal(new byte[] { 0x10 }, request);
            await stream.WriteAsync(new byte[] { 0x20 });
            Assert.Equal(new byte[] { 0x20 }, await responseTask);

            Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);
            await device.DisconnectAsync();
            Assert.Equal(ConnectionState.Disconnected, ((IConnection)device).State);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Legacy_tcp_client_reconnects_with_a_new_transport_generation_after_remote_close()
    {
        var reconnectOutcomes = new List<string?>();
        var reconnectMetric = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName &&
                    instrument.Name == "communication.reconnect.attempts")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "outcome")
                {
                    reconnectOutcomes.Add(tag.Value?.ToString());
                    reconnectMetric.TrySetResult();
                }
            }
        });
        meterListener.Start();

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            await using var device = new TcpClientDevice(new SocketParam
            {
                ServerIP = IPAddress.Loopback.ToString(),
                Port = port,
                Delimiter = null,
                TimeoutMs = 1000,
                AutoReconnect = true,
                ReconnectIntervalMs = 10,
            });
            var disconnected = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var reconnected = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var connectedCount = 0;
            device.ConnectionStateChanged += (_, args) =>
            {
                if (!args.IsConnected)
                {
                    disconnected.TrySetResult();
                }
                else if (Interlocked.Increment(ref connectedCount) == 2)
                {
                    reconnected.TrySetResult();
                }
            };

            var firstAccept = listener.AcceptTcpClientAsync();
            Assert.True(await device.ConnectAsync());
            using var firstPeer = await firstAccept;
            firstPeer.Dispose();
            await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(1));

            var secondAccept = listener.AcceptTcpClientAsync();
            using var secondPeer = await secondAccept.WaitAsync(TimeSpan.FromSeconds(1));
            await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(1));
            await reconnectMetric.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);
            Assert.Contains("success", reconnectOutcomes);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Slow_data_subscriber_does_not_block_request_response_completion()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var releaseSubscriber = new ManualResetEventSlim();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            await using var device = new TcpClientDevice(new SocketParam
            {
                ServerIP = IPAddress.Loopback.ToString(),
                Port = port,
                Delimiter = null,
                TimeoutMs = 1000,
                AutoReconnect = false,
            });
            try
            {
                var subscriberEntered = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                device.DataReceived += (_, _) =>
                {
                    subscriberEntered.TrySetResult();
                    releaseSubscriber.Wait();
                };

                var acceptTask = listener.AcceptTcpClientAsync();
                Assert.True(await device.ConnectAsync());
                using var peer = await acceptTask;
                await using var stream = peer.GetStream();

                var responseTask = device.SendAndReceiveAsync(new byte[] { 0x10 });
                var request = new byte[1];
                await stream.ReadExactlyAsync(request);
                await stream.WriteAsync(new byte[] { 0x20 });
                await subscriberEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));

                Assert.Equal(
                    new byte[] { 0x20 },
                    await responseTask.WaitAsync(TimeSpan.FromMilliseconds(200)));
            }
            finally
            {
                releaseSubscriber.Set();
                await device.DisposeAsync();
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Full_data_event_queue_drops_oldest_notification_and_emits_metric()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var releaseSubscriber = new ManualResetEventSlim();
        var subscriberBlocked = false;
        var queuedWhileBlocked = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var droppedEvent = new TaskCompletionSource<(
            string? EventType,
            string? Protocol,
            string? Transport)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName &&
                    instrument.Name is "communication.events.queued" or
                        "communication.events.dropped")
                {
                    currentListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "communication.events.queued" &&
                value == 1 && subscriberBlocked)
            {
                queuedWhileBlocked.TrySetResult();
            }
            if (instrument.Name == "communication.events.dropped")
            {
                string? eventType = null;
                string? protocol = null;
                string? transport = null;
                foreach (var tag in tags)
                {
                    switch (tag.Key)
                    {
                        case "event.type":
                            eventType = tag.Value?.ToString();
                            break;
                        case "protocol":
                            protocol = tag.Value?.ToString();
                            break;
                        case "transport":
                            transport = tag.Value?.ToString();
                            break;
                    }
                }
                droppedEvent.TrySetResult((eventType, protocol, transport));
            }
        });
        meterListener.Start();

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var device = new TcpClientDevice(new SocketParam
            {
                ServerIP = IPAddress.Loopback.ToString(),
                Port = port,
                Delimiter = null,
                AutoReconnect = false,
                EventQueueCapacity = 1,
            });
            try
            {
                var subscriberEntered = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                device.DataReceived += (_, _) =>
                {
                    subscriberBlocked = true;
                    subscriberEntered.TrySetResult();
                    releaseSubscriber.Wait();
                };

                var acceptTask = listener.AcceptTcpClientAsync();
                Assert.True(await device.ConnectAsync());
                using var peer = await acceptTask;
                await using var stream = peer.GetStream();

                await stream.WriteAsync(new byte[] { 0x01 });
                await subscriberEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
                await stream.WriteAsync(new byte[] { 0x02 });
                await queuedWhileBlocked.Task.WaitAsync(TimeSpan.FromSeconds(1));
                await stream.WriteAsync(new byte[] { 0x03 });

                Assert.Equal(
                    ("data_received", "Tcp", "tcp"),
                    await droppedEvent.Task.WaitAsync(TimeSpan.FromSeconds(1)));
            }
            finally
            {
                releaseSubscriber.Set();
                await device.DisposeAsync();
            }
        }
        finally
        {
            listener.Stop();
        }
    }
}
