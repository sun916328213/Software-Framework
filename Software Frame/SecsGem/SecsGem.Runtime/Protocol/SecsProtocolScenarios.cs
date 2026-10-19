using Secs4Net;
using SecsGem.Runtime.Order.S9;
using SecsGem.Runtime.Transport;

namespace SecsGem.Runtime.Protocol;

/// <summary>明确启用的业务策略；默认禁用，不暗中改变生产宿主的接收能力与会话规则。</summary>
public sealed record SecsProtocolScenarioOptions
{
    /// <summary>完整 SECS-II 正文编码字节上限；不含 HSMS 长度前缀和十字节报头。</summary>
    public int? MaxMessageBodyBytes { get; init; }
    /// <summary>S7F1 获准后等待同 PPID 的 S7F3 的时长；不同于 T3。</summary>
    public TimeSpan? ProcessProgramTransferTimeout { get; init; }
}

internal sealed class SecsProtocolScenarios : IDisposable
{
    private sealed class Transfer(string id)
    {
        internal readonly string Id = id;
        internal readonly CancellationTokenSource Cancellation = new();
        internal readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Transfer> _pending = new(StringComparer.Ordinal);
    private readonly HashSet<Transfer> _active = new();
    private readonly ISecsTransport _transport;
    private readonly Func<string, object, Task> _send;
    private readonly Action<string> _info;
    private readonly TimeProvider _time;
    private SecsProtocolScenarioOptions _options = new();
    private bool _disposed;

    internal SecsProtocolScenarios(ISecsTransport transport, Func<string, object, Task> send, Action<string> info, TimeProvider time)
    { _transport = transport; _send = send; _info = info; _time = time; }

    private void Report(string message)
    {
        try { _info(message); }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"{message}；诊断输出失败：{ex.Message}"); }
    }

    internal void Configure(SecsProtocolScenarioOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaxMessageBodyBytes is <= 0) throw new ArgumentOutOfRangeException(nameof(options.MaxMessageBodyBytes));
        if (options.ProcessProgramTransferTimeout is { } delay && (delay < TimeSpan.FromMilliseconds(1) || delay.TotalMilliseconds > uint.MaxValue - 1))
            throw new ArgumentOutOfRangeException(nameof(options.ProcessProgramTransferTimeout));
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_transport.IsConnected) throw new InvalidOperationException("请先断开连接再修改协议业务策略。");
            _options = options with { };
        }
    }

    internal void ObserveArrival(SecsMessage request)
    {
        // 期望消息已到达就结束会话等待；正文随后即使被 S9F7/F11 拒绝，也不谎报“未收到”。
        if (request.S != 7 || request.F != 3 || request.SecsItem is not { Format: SecsFormat.List, Count: > 0 } body
            || body.Items[0].Format != SecsFormat.ASCII) return;
        lock (_gate)
            if (_pending.Remove(body.Items[0].GetString(), out var transfer)) transfer.Cancellation.Cancel();
    }

    internal async Task<bool> RejectOversizedAsync(SecsMessage request)
    {
        if (request.S == 9 || _options.MaxMessageBodyBytes is not { } max) return false; // S9 不触发 S9 循环。
        if (EncodedSize(request.SecsItem, max) <= max) return false;
        if (!_transport.TryGetReceivedHeader(request, out var header)) throw new InvalidOperationException("超限消息缺少原始报头。");
        await _send("S9F11", new S9HeaderData(header)).ConfigureAwait(false);
        Report($"S9F11：S{request.S}F{request.F} 正文超过 {max} 字节，未执行业务处理。");
        return true;
    }

    internal void PrepareReply(SecsMessage request, Item? reply)
    {
        if (request.S != 7 || request.F != 1 || _options.MaxMessageBodyBytes is not { } max
            || reply is not { Format: SecsFormat.Binary, Count: 1 } || reply.GetMemory<byte>().Span[0] != 0) return;
        // Handler 已验证完整结构与 LENGTH 类型；询问时即可拒绝肯定装不下的下载。
        var items = request.SecsItem!.Items;
        ulong declared = items[1].Format switch
        {
            SecsFormat.U1 => items[1].GetMemory<byte>().Span[0],
            SecsFormat.U2 => items[1].GetMemory<ushort>().Span[0],
            SecsFormat.U4 => items[1].GetMemory<uint>().Span[0],
            _ => items[1].GetMemory<ulong>().Span[0]
        };
        ulong expected = 2UL + (ulong)EncodedSize(items[0], long.MaxValue) + declared + (declared > 65535 ? 4UL : declared > 255 ? 3UL : 2UL);
        if (expected <= (ulong)max) return;
        reply.GetMemory<byte>().Span[0] = 2; // PPGNT=2，长度许可拒绝，不误报 S9F11。
        Report($"S7F2：PPID={items[0].GetString()} 声明的下载超过 {max} 字节接收上限，PPGNT=2，不建立会话。");
    }

    internal void ObserveResult(SecsMessage request, Item? reply)
    {
        if (request.S != 7 || request.F != 1 || !request.ReplyExpected || !_transport.IsConnected
            || _options.ProcessProgramTransferTimeout is not { } timeout
            || reply is not { Format: SecsFormat.Binary, Count: 1 } || reply.GetMemory<byte>().Span[0] != 0
            || request.SecsItem is not { Format: SecsFormat.List, Count: 2 } body) return;
        var id = body.Items[0].GetString();
        Transfer transfer;
        lock (_gate)
        {
            if (_disposed || !_transport.IsConnected) return;
            if (_pending.Remove(id, out var old)) old.Cancellation.Cancel();
            transfer = new Transfer(id);
            _pending.Add(id, transfer);
            _active.Add(transfer);
        }
        _ = WatchAsync(transfer, timeout); // 不在锁内调用计时器或传输层。
        Report($"配方传输会话 PPID={id}：S7F1 已获准，等待 S7F3，期限 {timeout.TotalSeconds:g} 秒。");
    }

    private async Task WatchAsync(Transfer transfer, TimeSpan timeout)
    {
        try
        {
            await Task.Delay(timeout, _time, transfer.Cancellation.Token).ConfigureAwait(false);
            lock (_gate)
            {
                if (_disposed || transfer.Cancellation.IsCancellationRequested || !_transport.IsConnected
                    || !_pending.TryGetValue(transfer.Id, out var current) || !ReferenceEquals(current, transfer)) return;
                _pending.Remove(transfer.Id);
            }
            transfer.Cancellation.Token.ThrowIfCancellationRequested();
            await _send("S9F13", new SS9F13 { Order = "S7F3", Message = transfer.Id }).ConfigureAwait(false);
            Report($"S9F13：PPID={transfer.Id} 未在期限内发送 S7F3；会话已结束，不切换配方。迟到下载按独立请求处理。");
        }
        catch (OperationCanceledException) when (transfer.Cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!_disposed) Report($"配方传输会话 PPID={transfer.Id} 通知失败：{ex.Message}");
        }
        finally
        {
            lock (_gate)
            {
                _active.Remove(transfer);
                if (_pending.TryGetValue(transfer.Id, out var current) && ReferenceEquals(current, transfer)) _pending.Remove(transfer.Id);
                transfer.Cancellation.Dispose();
            }
            transfer.Completion.TrySetResult();
        }
    }

    internal void Reset()
    {
        lock (_gate)
        {
            _pending.Clear();
            foreach (var transfer in _active.ToArray()) transfer.Cancellation.Cancel();
        }
    }

    internal Task DrainAsync()
    {
        lock (_gate) return Task.WhenAll(_active.Select(transfer => transfer.Completion.Task));
    }

    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; Reset(); }
    }

    // 只计数，不额外编码/分配整个大报文；列表头长度按元素数，而非子项字节总数计算。
    internal static long EncodedSize(Item? item, long stopAfter)
    {
        if (item is null) return 0;
        long length = item.Format switch
        {
            SecsFormat.List => item.Count,
            SecsFormat.ASCII => Item.ASCIIEncoding.GetByteCount(item.GetString()),
            SecsFormat.JIS8 => Item.JIS8Encoding.GetByteCount(item.GetString()),
            SecsFormat.I2 or SecsFormat.U2 => (long)item.Count * 2,
            SecsFormat.I4 or SecsFormat.U4 or SecsFormat.F4 => (long)item.Count * 4,
            SecsFormat.I8 or SecsFormat.U8 or SecsFormat.F8 => (long)item.Count * 8,
            _ => item.Count
        };
        long size = length > 65535 ? 4 : length > 255 ? 3 : 2;
        if (item.Format != SecsFormat.List) return size + length;
        foreach (var child in item.Items)
        {
            size += EncodedSize(child, stopAfter);
            if (size > stopAfter) break;
        }
        return size;
    }
}
