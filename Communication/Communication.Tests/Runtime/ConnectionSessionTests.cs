using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Pipelines;
using CommunicationNew.Abstractions;
using CommunicationNew.Runtime;
using CommunicationNew.Transports;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CommunicationNew.Tests.Runtime;

[CollectionDefinition("Diagnostics", DisableParallelization = true)]
public sealed class DiagnosticsCollectionDefinition;

[Collection("Diagnostics")]
public sealed class ConnectionSessionTests
{
    [Fact]
    public async Task Connected_session_executes_a_request_and_stops_accepting_requests_after_disconnect()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));

        await session.ConnectAsync();
        Assert.Equal(ConnectionState.Connected, session.State);

        var responseTask = session.ExecuteAsync(0x2A).AsTask();
        Assert.Equal(0x2A, await transport.ReadRequestAsync());

        await transport.WriteResponseAsync(0x7B);
        Assert.Equal(0x7B, await responseTask);

        await session.DisconnectAsync();
        Assert.Equal(ConnectionState.Disconnected, session.State);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.ExecuteAsync(0x01).AsTask());
    }

    [Fact]
    public async Task Operation_timeout_is_reported_separately_from_caller_cancellation()
    {
        var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromMilliseconds(20));
        await session.ConnectAsync();

        await Assert.ThrowsAsync<TimeoutException>(() => session.ExecuteAsync(0x2A).AsTask());

        using var callerCts = new CancellationTokenSource();
        callerCts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.ExecuteAsync(0x2A, callerCts.Token).AsTask());
    }

    [Fact]
    public async Task Concurrent_connect_and_repeated_disconnect_are_serialized_and_idempotent()
    {
        var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));

        await Task.WhenAll(
            session.ConnectAsync().AsTask(),
            session.ConnectAsync().AsTask());
        Assert.Equal(ConnectionState.Connected, session.State);

        await session.DisconnectAsync();
        await session.DisconnectAsync();
        Assert.Equal(ConnectionState.Disconnected, session.State);
    }

    [Fact]
    public async Task Failed_transport_close_still_leaves_session_disconnected()
    {
        var transport = new ScriptedByteStreamTransport { FailNextClose = true };
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        await session.ConnectAsync();

        await Assert.ThrowsAsync<IOException>(() => session.DisconnectAsync().AsTask());

        Assert.Equal(ConnectionState.Disconnected, session.State);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.ExecuteAsync(0x01).AsTask());
    }

    [Fact]
    public async Task Dispose_releases_transport_even_when_disconnect_fails()
    {
        var transport = new ScriptedByteStreamTransport { FailNextClose = true };
        var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        await session.ConnectAsync();

        await Assert.ThrowsAsync<IOException>(() => session.DisposeAsync().AsTask());

        Assert.True(transport.IsDisposed);
        Assert.Equal(ConnectionState.Disposed, session.State);
    }

    [Fact]
    public async Task Concurrent_dispose_calls_share_completion_and_release_transport_once()
    {
        var transport = new ScriptedByteStreamTransport();
        var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        transport.PauseDispose();

        var firstDispose = session.DisposeAsync().AsTask();
        await transport.WaitForDisposeStartedAsync();
        var secondDispose = session.DisposeAsync().AsTask();

        try
        {
            Assert.Same(firstDispose, secondDispose);
            Assert.Equal(1, transport.DisposeCount);
        }
        finally
        {
            transport.ResumeDispose();
        }

        await Task.WhenAll(firstDispose, secondDispose);
        Assert.True(transport.IsDisposed);
        Assert.Equal(ConnectionState.Disposed, session.State);
    }

    [Fact]
    public async Task Connect_started_during_dispose_is_rejected()
    {
        var transport = new ScriptedByteStreamTransport();
        var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        transport.PauseDispose();

        var disposeTask = session.DisposeAsync().AsTask();
        await transport.WaitForDisposeStartedAsync();
        var connectTask = session.ConnectAsync().AsTask();

        try
        {
            await Assert.ThrowsAsync<ObjectDisposedException>(() => connectTask);
        }
        finally
        {
            transport.ResumeDispose();
        }

        await disposeTask;
        Assert.Equal(ConnectionState.Disposed, session.State);
        Assert.False(transport.IsOpen);
    }

    [Fact]
    public async Task Successful_request_emits_standard_activity_with_outcome()
    {
        Activity? stoppedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CommunicationDiagnostics.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = activity => stoppedActivity = activity,
        };
        ActivitySource.AddActivityListener(listener);
        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        await session.ConnectAsync();

        var responseTask = session.ExecuteAsync(0x2A).AsTask();
        await transport.ReadRequestAsync();
        await transport.WriteResponseAsync(0x7B);
        await responseTask;

        Assert.NotNull(stoppedActivity);
        Assert.Equal("communication.request", stoppedActivity.OperationName);
        Assert.Equal("success", stoppedActivity.GetTagItem("outcome"));
    }

    [Fact]
    public async Task Request_trace_contains_transport_protocol_and_pipeline_phases()
    {
        var stoppedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CommunicationDiagnostics.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = stoppedActivities.Add,
        };
        ActivitySource.AddActivityListener(listener);
        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        await session.ConnectAsync();

        var responseTask = session.ExecuteAsync(0x2A).AsTask();
        await transport.ReadRequestAsync();
        await transport.WriteResponseAsync(0x7B);
        await responseTask;

        var requestActivity = Assert.Single(
            stoppedActivities,
            activity => activity.OperationName == "communication.request");
        Assert.Equal("SingleByteCodec", requestActivity.GetTagItem("protocol"));
        Assert.Equal(
            "ScriptedByteStreamTransport",
            requestActivity.GetTagItem("transport"));
        Assert.Equal(
            new[]
            {
                "communication.decode",
                "communication.encode",
                "communication.queue",
                "communication.send",
                "communication.wait",
            },
            stoppedActivities
                .Where(activity => activity.ParentId == requestActivity.Id)
                .Select(activity => activity.OperationName)
                .Order());
    }

    [Fact]
    public async Task Session_emits_connection_and_byte_metrics_through_standard_meter()
    {
        var measurements = new List<(string Name, long Value)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>(
            (instrument, value, _, _) => measurements.Add((instrument.Name, value)));
        listener.Start();

        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        await session.ConnectAsync();
        var responseTask = session.ExecuteAsync(0x2A).AsTask();
        await transport.ReadRequestAsync();
        await transport.WriteResponseAsync(0x7B);
        await responseTask;
        await session.DisconnectAsync();

        Assert.Equal(
            new long[] { 1, -1 },
            measurements
                .Where(item => item.Name == "communication.connections.active")
                .Select(item => item.Value));
        Assert.Equal(
            3,
            Assert.Single(measurements, item => item.Name == "communication.bytes.sent").Value);
        Assert.Equal(
            3,
            Assert.Single(measurements, item => item.Name == "communication.bytes.received").Value);
    }

    [Fact]
    public async Task Request_metric_contains_protocol_and_transport_tags()
    {
        var operationTags = new List<KeyValuePair<string, object?>>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName &&
                    instrument.Name == "communication.operations")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                operationTags.Add(tag);
            }
        });
        listener.Start();

        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        await session.ConnectAsync();
        var responseTask = session.ExecuteAsync(0x2A).AsTask();
        await transport.ReadRequestAsync();
        await transport.WriteResponseAsync(0x7B);
        await responseTask;

        Assert.Contains(
            operationTags,
            tag => tag.Key == "protocol" && Equals(tag.Value, "SingleByteCodec"));
        Assert.Contains(
            operationTags,
            tag => tag.Key == "transport" &&
                Equals(tag.Value, "ScriptedByteStreamTransport"));
    }

    [Fact]
    public async Task Failed_request_emits_faulted_outcome_and_duration_metrics()
    {
        var operationOutcomes = new List<string?>();
        var durations = new List<double>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName &&
                    instrument.Name is "communication.operations" or
                        "communication.operation.duration")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (instrument.Name == "communication.operations")
            {
                operationOutcomes.Add(GetTagValue(tags, "outcome"));
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
        {
            // Histogram 使用 double 毫秒值，与操作计数分别注册回调。
            if (instrument.Name == "communication.operation.duration")
            {
                durations.Add(value);
            }
        });
        listener.Start();

        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        await session.ConnectAsync();

        var responseTask = session.ExecuteAsync(0x2A).AsTask();
        await transport.ReadRequestAsync();
        await transport.CompleteResponsesAsync();
        await Assert.ThrowsAsync<EndOfStreamException>(() => responseTask);

        Assert.Equal("faulted", Assert.Single(operationOutcomes));
        Assert.True(Assert.Single(durations) >= 0);
    }

    [Fact]
    public async Task Waiting_request_emits_queue_depth_until_it_gets_execution_ownership()
    {
        var queueMeasurements = new List<long>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName &&
                    instrument.Name == "communication.requests.queued")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>(
            (_, value, _, _) => queueMeasurements.Add(value));
        listener.Start();

        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        await session.ConnectAsync();

        var first = session.ExecuteAsync(0x2A).AsTask();
        Assert.Equal(0x2A, await transport.ReadRequestAsync());
        var second = session.ExecuteAsync(0x2B).AsTask();
        Assert.Equal(new long[] { 1 }, queueMeasurements);

        await transport.WriteResponseAsync(0x7A, correlationId: 1);
        await first;
        Assert.Equal(0x2B, await transport.ReadRequestAsync());
        await transport.WriteResponseAsync(0x7B, correlationId: 2);
        await second;

        Assert.Equal(new long[] { 1, -1 }, queueMeasurements);
    }

    [Fact]
    public async Task Request_queue_rejects_new_work_when_its_capacity_is_reached()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1),
            maxQueuedRequests: 2);
        await session.ConnectAsync();

        var active = session.ExecuteAsync(0x10).AsTask();
        Assert.Equal(0x10, await transport.ReadRequestAsync());
        var waiting1 = session.ExecuteAsync(0x11).AsTask();
        var waiting2 = session.ExecuteAsync(0x12).AsTask();

        await Assert.ThrowsAsync<RequestQueueFullException>(() =>
            session.ExecuteAsync(0x13).AsTask());

        await transport.WriteResponseAsync(0x20, correlationId: 1);
        Assert.Equal(0x20, await active);
        await transport.ReadRequestAsync();
        await transport.WriteResponseAsync(0x21, correlationId: 2);
        await transport.ReadRequestAsync();
        await transport.WriteResponseAsync(0x22, correlationId: 3);
        await Task.WhenAll(waiting1, waiting2);
    }

    [Fact]
    public async Task Session_reports_connection_state_changes_through_standard_logger()
    {
        var logger = new RecordingLogger();
        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1),
            logger: logger);

        await session.ConnectAsync();
        await session.DisconnectAsync();

        Assert.Contains(logger.Messages, message => message.Contains("Connected"));
        Assert.Contains(logger.Messages, message => message.Contains("Disconnected"));
    }

    [Fact]
    public async Task Failed_connect_emits_faulted_connection_attempt_metric()
    {
        var attempts = new List<(long Value, string? Outcome)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName &&
                    instrument.Name == "communication.connection.attempts")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            string? outcome = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "outcome") outcome = tag.Value?.ToString();
            }
            attempts.Add((value, outcome));
        });
        listener.Start();

        await using var transport = new ScriptedByteStreamTransport { FailNextOpen = true };
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<IOException>(() => session.ConnectAsync().AsTask());

        Assert.Equal((1, "faulted"), Assert.Single(attempts));
    }

    [Fact]
    public async Task Remote_close_faults_session_and_releases_active_connection_metric()
    {
        var activeConnections = new List<long>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName &&
                    instrument.Name == "communication.connections.active")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>(
            (_, value, _, _) => activeConnections.Add(value));
        listener.Start();

        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        await session.ConnectAsync();

        var responseTask = session.ExecuteAsync(0x2A).AsTask();
        await transport.ReadRequestAsync();
        await transport.CompleteResponsesAsync();

        await Assert.ThrowsAsync<EndOfStreamException>(() => responseTask);
        Assert.Equal(ConnectionState.Faulted, session.State);
        Assert.Equal(new long[] { 1, -1 }, activeConnections);
    }

    [Fact]
    public async Task Enabled_reconnect_supervisor_restores_connection_after_remote_close()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1),
            reconnectOptions: new ConnectionReconnectOptions(TimeSpan.Zero));
        await session.ConnectAsync();
        await transport.WaitForOpenAsync();

        var failedResponse = session.ExecuteAsync(0x2A).AsTask();
        await transport.ReadRequestAsync();
        await transport.CompleteResponsesAsync();
        await Assert.ThrowsAsync<EndOfStreamException>(() => failedResponse);

        await transport.WaitForOpenAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(ConnectionState.Connected, session.State);

        var recoveredResponse = session.ExecuteAsync(0x2B).AsTask();
        Assert.Equal(0x2B, await transport.ReadRequestAsync());
        await transport.WriteResponseAsync(0x7B, correlationId: 2);
        Assert.Equal(0x7B, await recoveredResponse);
    }

    [Fact]
    public async Task Reconnect_delay_uses_injected_time_provider_without_waiting_real_time()
    {
        var reconnectDelay = TimeSpan.FromDays(1);
        var timeProvider = new ManualTimerTimeProvider();
        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromHours(1),
            timeProvider,
            reconnectOptions: new ConnectionReconnectOptions(reconnectDelay));
        await session.ConnectAsync();
        await transport.WaitForOpenAttemptAsync();

        var failedResponse = session.ExecuteAsync(0x2A).AsTask();
        await transport.ReadRequestAsync();
        await transport.CompleteResponsesAsync();
        await Assert.ThrowsAsync<EndOfStreamException>(() => failedResponse);
        await timeProvider.WaitUntilScheduledAsync(reconnectDelay)
            .WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(ConnectionState.Faulted, session.State);
        timeProvider.Fire(reconnectDelay);
        await transport.WaitForOpenAttemptAsync().AsTask()
            .WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(ConnectionState.Connected, session.State);
    }

    [Fact]
    public async Task Manual_disconnect_cancels_and_waits_for_reconnect_supervisor()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1),
            reconnectOptions: new ConnectionReconnectOptions(TimeSpan.Zero));
        await session.ConnectAsync();
        await transport.WaitForOpenAttemptAsync();
        transport.BlockNextOpen = true;

        var failedResponse = session.ExecuteAsync(0x2A).AsTask();
        await transport.ReadRequestAsync();
        await transport.CompleteResponsesAsync();
        await Assert.ThrowsAsync<EndOfStreamException>(() => failedResponse);
        await transport.WaitForOpenAttemptAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

        await session.DisconnectAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(ConnectionState.Disconnected, session.State);
        Assert.False(transport.IsOpen);
    }

    [Fact]
    public async Task Manual_connect_from_faulted_state_replaces_failed_transport_generation()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        await session.ConnectAsync();

        var failedResponse = session.ExecuteAsync(0x2A).AsTask();
        await transport.ReadRequestAsync();
        await transport.CompleteResponsesAsync();
        await Assert.ThrowsAsync<EndOfStreamException>(() => failedResponse);

        await session.ConnectAsync();

        Assert.Equal(ConnectionState.Connected, session.State);
        var recoveredResponse = session.ExecuteAsync(0x2B).AsTask();
        Assert.Equal(0x2B, await transport.ReadRequestAsync());
        await transport.WriteResponseAsync(0x7B, correlationId: 2);
        Assert.Equal(0x7B, await recoveredResponse);
    }

    [Fact]
    public async Task Zero_delay_reconnect_failures_do_not_block_fault_delivery_or_disconnect()
    {
        await using var transport = new ScriptedByteStreamTransport
        {
            FailSubsequentOpens = true,
        };
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1),
            reconnectOptions: new ConnectionReconnectOptions(TimeSpan.Zero));
        await session.ConnectAsync();

        var failedResponse = session.ExecuteAsync(0x2A).AsTask();
        await transport.ReadRequestAsync();
        await transport.CompleteResponsesAsync();

        await Assert.ThrowsAsync<EndOfStreamException>(
            () => failedResponse.WaitAsync(TimeSpan.FromSeconds(1)));
        await session.DisconnectAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(ConnectionState.Disconnected, session.State);
    }

    private sealed class SingleByteCodec : IRequestCodec<byte, byte>
    {
        public ReadOnlyMemory<byte> Encode(byte request, ushort correlationId)
        {
            return new byte[] { (byte)(correlationId >> 8), (byte)correlationId, request };
        }

        public bool TryDecode(
            ReadOnlySequence<byte> buffer,
            ushort correlationId,
            out byte response,
            out SequencePosition consumed)
        {
            response = default;
            consumed = buffer.Start;
            if (buffer.Length < 3)
            {
                return false;
            }

            Span<byte> frame = stackalloc byte[3];
            buffer.Slice(0, 3).CopyTo(frame);
            var actualCorrelationId = (ushort)((frame[0] << 8) | frame[1]);
            if (actualCorrelationId != correlationId)
            {
                throw new InvalidDataException("Unexpected correlation id.");
            }

            response = frame[2];
            consumed = buffer.GetPosition(3);
            return true;
        }
    }

    private static string? GetTagValue(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        string name)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == name)
            {
                return tag.Value?.ToString();
            }
        }

        return null;
    }

    private sealed class ScriptedByteStreamTransport : IByteStreamTransport
    {
        private Pipe _requests = new();
        private Pipe _responses = new();
        private readonly SemaphoreSlim _opened = new(0);
        private readonly SemaphoreSlim _openAttempts = new(0);
        private readonly TaskCompletionSource _disposeStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _resumeDispose =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _openCount;
        private bool _pauseDispose;

        public PipeReader Input => _responses.Reader;
        public PipeWriter Output => _requests.Writer;
        public bool IsOpen { get; private set; }
        public bool FailNextOpen { get; set; }
        public bool FailSubsequentOpens { get; set; }
        public bool BlockNextOpen { get; set; }
        public bool FailNextClose { get; set; }
        public bool IsDisposed { get; private set; }
        public int DisposeCount { get; private set; }

        public async ValueTask OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _openAttempts.Release();
            if (FailNextOpen)
            {
                FailNextOpen = false;
                throw new IOException("Scripted open failure.");
            }
            if (FailSubsequentOpens && _openCount > 0)
            {
                throw new IOException("Scripted reconnect failure.");
            }
            if (IsOpen)
            {
                throw new InvalidOperationException("Transport was opened twice.");
            }
            if (BlockNextOpen)
            {
                BlockNextOpen = false;
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (_openCount > 0)
            {
                _requests = new Pipe();
                _responses = new Pipe();
            }
            IsOpen = true;
            _openCount++;
            _opened.Release();
        }

        public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            if (FailNextClose)
            {
                FailNextClose = false;
                throw new IOException("Scripted close failure.");
            }
            await _requests.Writer.CompleteAsync();
            await _responses.Writer.CompleteAsync();
        }

        public async ValueTask<byte> ReadRequestAsync()
        {
            var read = await _requests.Reader.ReadAsync();
            try
            {
                var frame = read.Buffer.Slice(0, 3).ToArray();
                return frame[2];
            }
            finally
            {
                _requests.Reader.AdvanceTo(read.Buffer.GetPosition(3));
            }
        }

        public async ValueTask WriteResponseAsync(byte response, ushort correlationId = 1)
        {
            await _responses.Writer.WriteAsync(new byte[]
            {
                (byte)(correlationId >> 8),
                (byte)correlationId,
                response,
            });
        }

        public ValueTask CompleteResponsesAsync() => _responses.Writer.CompleteAsync();

        public ValueTask WaitForOpenAsync() => new(_opened.WaitAsync());

        public ValueTask WaitForOpenAttemptAsync() => new(_openAttempts.WaitAsync());

        public void PauseDispose() => _pauseDispose = true;

        public Task WaitForDisposeStartedAsync() => _disposeStarted.Task;

        public void ResumeDispose() => _resumeDispose.TrySetResult();

        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            _disposeStarted.TrySetResult();
            if (_pauseDispose)
            {
                await _resumeDispose.Task;
            }

            await CloseAsync();
            await _requests.Reader.CompleteAsync();
            await _responses.Reader.CompleteAsync();
            _opened.Dispose();
            _openAttempts.Dispose();
            IsDisposed = true;
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    /// <summary>
    /// 仅在测试主动触发时执行计时器回调，用于证明 Runtime 没有绕过注入的时间源。
    /// </summary>
    private sealed class ManualTimerTimeProvider : TimeProvider
    {
        private readonly object _sync = new();
        private readonly List<ManualTimer> _timers = new();
        private readonly Dictionary<TimeSpan, TaskCompletionSource> _scheduled = new();

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, dueTime, period);
            TaskCompletionSource? scheduled;
            lock (_sync)
            {
                _timers.Add(timer);
                _scheduled.TryGetValue(dueTime, out scheduled);
            }

            scheduled?.TrySetResult();
            return timer;
        }

        public Task WaitUntilScheduledAsync(TimeSpan dueTime)
        {
            lock (_sync)
            {
                if (_timers.Any(timer =>
                    !timer.IsDisposed && timer.DueTime == dueTime))
                {
                    return Task.CompletedTask;
                }

                if (!_scheduled.TryGetValue(dueTime, out var scheduled))
                {
                    scheduled = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    _scheduled.Add(dueTime, scheduled);
                }

                return scheduled.Task;
            }
        }

        public void Fire(TimeSpan dueTime)
        {
            ManualTimer timer;
            lock (_sync)
            {
                timer = _timers.Last(candidate =>
                    !candidate.IsDisposed && candidate.DueTime == dueTime);
            }

            timer.Fire();
        }

        private sealed class ManualTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period) : ITimer
        {
            private int _disposed;

            public TimeSpan DueTime { get; private set; } = dueTime;

            public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

            public bool Change(TimeSpan newDueTime, TimeSpan newPeriod)
            {
                if (IsDisposed)
                {
                    return false;
                }

                DueTime = newDueTime;
                period = newPeriod;
                return true;
            }

            public void Fire()
            {
                if (period == Timeout.InfiniteTimeSpan)
                {
                    Interlocked.Exchange(ref _disposed, 1);
                }

                callback(state);
            }

            public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
