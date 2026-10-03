// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：PlcService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Buffers.Binary、System.Text、Communication.Core.Abstractions、Communication.Core.Converters、Communication.Core.Protocols.Mitsubishi、Communication.Runtime.Protocols.Mitsubishi。导入不代表所有类型都会在运行时加载。
using System.Buffers.Binary;
using System.Text;
using Communication.Core.Abstractions;
using Communication.Core.Converters;
using Communication.Core.Protocols.Mitsubishi;
using Communication.Runtime.Protocols.Mitsubishi;

namespace MachineApp.Infra.Plc;

/// <summary>
/// Packing Machine 唯一的 PLC 接入点：独占持有 <see cref="McClientDevice"/>，
/// 提供连接/断开、类型化读写、连续清零、心跳和生命周期管理。
/// <para>
/// 单位冻结（旧语义）：所有寄存器数量均按 16-bit 点数；McClientDevice 的线格式
/// 转换（MC 小端 ↔ 大端）已由其内部完成，本类只处理“多个 word 如何组成
/// Int/Float/Double/String”的应用语义，32-bit 字顺序走集中配置的
/// <see cref="DataFormatCodec"/>，禁止手写 BitConverter 拼接。
/// </para>
/// <para>线程安全：设备操作经单信号量串行；心跳与业务调用可并发。</para>
/// </summary>
public sealed class PlcService : IDisposable, IAsyncDisposable
{
    private readonly PlcOptions _options;
    private readonly DataFormat _format;
    private readonly DataFormatCodec _codec;
    private readonly McClientDevice _device;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Timer _heartbeat;
    private int _heartbeatRunning;
    private int _disposed;
    private volatile bool _isConnected;

    /// <summary>连接状态变化（true=Online，false=Offline），供业务门面记录日志。</summary>
    public event Action<bool>? ConnectionChanged;

    /// <summary>
    /// 初始化 PlcService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof、ParsedDataFormat、ToMcParam、Max、FromMilliseconds；具体条件和先后次序见方法体。
    /// </summary>
    public PlcService(PlcOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _format = options.ParsedDataFormat();
        _codec = new DataFormatCodec(_format);
        _device = new McClientDevice(options.ToMcParam());
        var interval = Math.Max(100, options.HeartbeatIntervalMs);
        _heartbeat = new Timer(OnHeartbeat, null, Timeout.Infinite, Timeout.Infinite);
        HeartbeatInterval = TimeSpan.FromMilliseconds(interval);
    }

    /// <summary>心跳间隔（钳制后）。</summary>
    public TimeSpan HeartbeatInterval { get; }

    /// <summary>握手等待超时（毫秒，来自 PLC.json）。</summary>
    public int HandshakeTimeoutMs => _options.HandshakeTimeoutMs;

    /// <summary>当前是否在线（最后已知状态）。</summary>
    public bool IsConnected => _isConnected && _disposed == 0;

    /// <summary>幂等连接；已连接直接返回 true。</summary>
    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_device.State == ConnectionState.Connected)
            {
                SetConnected(true);
                return true;
            }

            var ok = await _device.ConnectAsync(ct).ConfigureAwait(false);
            SetConnected(ok);
            return ok;
        }
        catch (OperationCanceledException)
        {
            SetConnected(false);
            throw;
        }
        catch
        {
            SetConnected(false);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>幂等断开；重复调用安全。</summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            try
            {
                await _device.DisconnectAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // 断开阶段的异常不应掩盖调用方流程，状态照常置 Offline。
            }
            finally
            {
                SetConnected(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>启动 Online 心跳：掉线自动重连尝试，异常仅标记 Offline。</summary>
    public void StartHeartbeat()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        _heartbeat.Change(HeartbeatInterval, HeartbeatInterval);
    }

    /// <summary>停止心跳；重复调用安全。</summary>
    public void StopHeartbeat()
    {
        _heartbeat.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>读取 1 个寄存器并按 Int16 解码。</summary>
    public async Task<short> ReadInt16Async(string address, CancellationToken ct = default)
    {
        var bytes = await ReadRegistersAsync(address, 1, ct).ConfigureAwait(false);
        return _codec.DecodeInt16(bytes);
    }

    /// <summary>读取 2 个寄存器并按集中配置的 <see cref="DataFormat"/> 解码 Int32。</summary>
    public async Task<int> ReadInt32Async(string address, CancellationToken ct = default)
    {
        var bytes = await ReadRegistersAsync(address, 2, ct).ConfigureAwait(false);
        return _codec.DecodeInt32(bytes);
    }

    /// <summary>读取 2 个寄存器并按集中配置的 <see cref="DataFormat"/> 解码单精度浮点。</summary>
    public async Task<float> ReadSingleAsync(string address, CancellationToken ct = default)
    {
        var bytes = await ReadRegistersAsync(address, 2, ct).ConfigureAwait(false);
        return _codec.DecodeSingle(bytes);
    }

    /// <summary>读取 4 个寄存器并解码双精度浮点（仅支持 ABCD/DCBA，其余抛错）。</summary>
    public async Task<double> ReadDoubleAsync(string address, CancellationToken ct = default)
    {
        var bytes = await ReadRegistersAsync(address, 4, ct).ConfigureAwait(false);
        return BitConverter.Int64BitsToDouble(DecodeInt64(bytes));
    }

    /// <summary>按固定寄存器长度读取 ASCII 字符串（word 内大端即字符顺序，尾部 0 截断）。</summary>
    public async Task<string> ReadStringAsync(string address, int registerCount, CancellationToken ct = default)
    {
        if (registerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(registerCount));
        }

        var bytes = await ReadRegistersAsync(address, registerCount, ct).ConfigureAwait(false);
        return DecodeAscii(bytes);
    }

    /// <summary>写入 Int16（1 个寄存器）。</summary>
    public Task WriteInt16Async(string address, short value, CancellationToken ct = default) =>
        WriteBytesAsync(address, _codec.EncodeInt16(value), ct);

    /// <summary>写入 Int32（2 个寄存器，字顺序走集中配置）。</summary>
    public Task WriteInt32Async(string address, int value, CancellationToken ct = default) =>
        WriteBytesAsync(address, _codec.EncodeInt32(value), ct);

    /// <summary>写入单精度浮点（2 个寄存器，字顺序走集中配置）。</summary>
    public Task WriteSingleAsync(string address, float value, CancellationToken ct = default) =>
        WriteBytesAsync(address, _codec.EncodeSingle(value), ct);

    /// <summary>写入双精度浮点（4 个寄存器，仅支持 ABCD/DCBA）。</summary>
    public Task WriteDoubleAsync(string address, double value, CancellationToken ct = default) =>
        WriteBytesAsync(address, EncodeInt64(BitConverter.DoubleToInt64Bits(value)), ct);

    /// <summary>
    /// 按固定寄存器长度写入 ASCII：不足补 0，超长明确拒绝（不静默截断/溢出）。
    /// </summary>
    public Task WriteStringAsync(string address, string value, int registerCount, CancellationToken ct = default)
    {
        if (registerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(registerCount));
        }

        return WriteBytesAsync(address, EncodeAscii(value ?? string.Empty, registerCount * 2), ct);
    }

    /// <summary>按值长度写入 ASCII（偶数字节对齐，用于无 Length 的动态 String 点位）。</summary>
    public Task WriteDynamicStringAsync(string address, string value, CancellationToken ct = default)
    {
        var raw = Encoding.ASCII.GetBytes(value ?? string.Empty);
        var even = raw.Length + (raw.Length & 1);
        return WriteBytesAsync(address, EncodeAscii(value ?? string.Empty, even), ct);
    }

    /// <summary>向连续寄存器清零（Reset 语义：length 个 0 word）。</summary>
    public Task ResetAsync(string address, int registerCount, CancellationToken ct = default)
    {
        if (registerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(registerCount));
        }

        return WriteBytesAsync(address, new byte[checked(registerCount * 2)], ct);
    }

    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadRegistersAsync。
    /// 正文中的关键协作调用包括 ThrowIf、ConfigureAwait、WaitAsync、ReadAsync、SetConnected；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task<byte[]> ReadRegistersAsync(string address, int registerCount, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var bytes = await _device.ReadAsync(address, registerCount, ct).ConfigureAwait(false);
            SetConnected(true);
            return bytes;
        }
        catch
        {
            SetConnected(false);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 执行写入操作；对应入口为 WriteBytesAsync。
    /// 正文中的关键协作调用包括 ThrowIf、ConfigureAwait、WaitAsync、WriteAsync、SetConnected；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task WriteBytesAsync(string address, byte[] bytes, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _device.WriteAsync(address, bytes, ct).ConfigureAwait(false);
            SetConnected(true);
        }
        catch
        {
            SetConnected(false);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnHeartbeat。
    /// 正文中的关键协作调用包括 Exchange、ContinueWith、TickAsync；具体条件和先后次序见方法体。
    /// </summary>
    private void OnHeartbeat(object? _)
    {
        if (Interlocked.Exchange(ref _heartbeatRunning, 1) != 0)
        {
            return;
        }

        _ = TickAsync().ContinueWith(
            _ => Interlocked.Exchange(ref _heartbeatRunning, 0),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 TickAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task TickAsync()
    {
        if (_disposed != 0)
        {
            return;
        }

        try
        {
            if (!IsConnected)
            {
                await ConnectAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 心跳不使用取消：此处仅防御性捕获。
        }
        catch
        {
            // 心跳失败只标记 Offline，不抛向 Timer 线程；状态变化由事件通知业务记录。
        }
    }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetConnected。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private void SetConnected(bool value)
    {
        if (_isConnected == value)
        {
            return;
        }

        _isConnected = value;
        try
        {
            ConnectionChanged?.Invoke(value);
        }
        catch
        {
            // 业务订阅者的日志异常不能破坏连接状态机。
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 DecodeAscii。
    /// 正文中的关键协作调用包括 GetString、TrimEnd；具体条件和先后次序见方法体。
    /// </summary>
    private static string DecodeAscii(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.ASCII.GetString(bytes);
        return text.TrimEnd('\0');
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EncodeAscii。
    /// 正文中的关键协作调用包括 GetBytes、nameof、BlockCopy；具体条件和先后次序见方法体。
    /// </summary>
    private static byte[] EncodeAscii(string value, int byteCount)
    {
        var raw = Encoding.ASCII.GetBytes(value);
        if (raw.Length > byteCount)
        {
            throw new ArgumentException(
                $"字符串字节长度 {raw.Length} 超出固定长度 {byteCount}，拒绝写入（不静默截断）。",
                nameof(value));
        }

        var destination = new byte[byteCount];
        Buffer.BlockCopy(raw, 0, destination, 0, raw.Length);
        return destination;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 DecodeInt64。
    /// 正文中的关键协作调用包括 nameof、CopyTo、ReadInt64BigEndian；具体条件和先后次序见方法体。
    /// </summary>
    private long DecodeInt64(ReadOnlySpan<byte> source)
    {
        // Double 当前仅支持 ABCD/DCBA；CSV 暂无 Double 点位，本分支平时不可达。
        if (source.Length < 8)
        {
            throw new ArgumentException("64 位数值需要八个字节。", nameof(source));
        }

        Span<byte> canonical = stackalloc byte[8];
        switch (_format)
        {
            case DataFormat.ABCD:
                source[..8].CopyTo(canonical);
                break;
            case DataFormat.DCBA:
                for (var i = 0; i < 8; i++)
                {
                    canonical[i] = source[7 - i];
                }

                break;
            default:
                throw new NotSupportedException($"Double 不支持数据格式 {_format}，仅支持 ABCD/DCBA。");
        }

        return BinaryPrimitives.ReadInt64BigEndian(canonical);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EncodeInt64。
    /// 正文中的关键协作调用包括 WriteInt64BigEndian、CopyTo；具体条件和先后次序见方法体。
    /// </summary>
    private byte[] EncodeInt64(long value)
    {
        Span<byte> canonical = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(canonical, value);

        var destination = new byte[8];
        switch (_format)
        {
            case DataFormat.ABCD:
                canonical.CopyTo(destination);
                break;
            case DataFormat.DCBA:
                for (var i = 0; i < 8; i++)
                {
                    destination[i] = canonical[7 - i];
                }

                break;
            default:
                throw new NotSupportedException($"Double 不支持数据格式 {_format}，仅支持 ABCD/DCBA。");
        }

        return destination;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 Exchange、StopHeartbeat、ConfigureAwait、DisconnectAsync、Dispose；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        StopHeartbeat();
        try
        {
            await DisconnectAsync().ConfigureAwait(false);
        }
        catch
        {
            // 释放阶段不抛错。
        }

        _device.Dispose();
        _heartbeat.Dispose();
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 GetResult、GetAwaiter、AsTask、DisposeAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
