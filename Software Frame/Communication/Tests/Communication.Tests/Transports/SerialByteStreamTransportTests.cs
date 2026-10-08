// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SerialByteStreamTransportTests、LoopbackSerialConnection、LoopbackStream。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Buffers、System.IO.Pipelines、Communication.Runtime.Transports、Xunit。导入不代表所有类型都会在运行时加载。
using System.Buffers;
using System.IO.Pipelines;
using Communication.Runtime.Transports;
using Xunit;

namespace Communication.Tests.Transports;

/// <summary>
/// SerialByteStreamTransportTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class SerialByteStreamTransportTests
{
    /// <summary>
    /// 测试相关入口 Repeated_open_is_idempotent；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 OpenAsync、True、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Repeated_open_is_idempotent()
    {
        var connection = new LoopbackSerialConnection();
        await using var transport = new SerialByteStreamTransport(
            new SerialTransportOptions("COM_TEST", 9600),
            connection);

        await transport.OpenAsync();
        await transport.OpenAsync();

        Assert.True(transport.IsOpen);
        Assert.Equal(1, connection.OpenCount);
    }

    /// <summary>
    /// 测试相关入口 Open_transport_exposes_duplex_stream_and_close_is_idempotent；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 OpenAsync、WriteAsync、ReadAsync、ToArray、AdvanceTo；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Open_transport_exposes_duplex_stream_and_close_is_idempotent()
    {
        var connection = new LoopbackSerialConnection();
        await using var transport = new SerialByteStreamTransport(
            new SerialTransportOptions("COM_TEST", 9600),
            connection);

        await transport.OpenAsync();
        await transport.Output.WriteAsync(new byte[] { 0x31, 0x32 });
        var read = await transport.Input.ReadAsync();
        var payload = read.Buffer.ToArray();
        transport.Input.AdvanceTo(read.Buffer.End);

        Assert.Equal(new byte[] { 0x31, 0x32 }, payload);
        Assert.True(transport.IsOpen);
        Assert.Equal(1, connection.OpenCount);

        await transport.CloseAsync();
        await transport.CloseAsync();
        Assert.False(transport.IsOpen);
        Assert.Equal(1, connection.CloseCount);
    }

    /// <summary>
    /// 测试相关入口 Concurrent_dispose_calls_release_the_serial_connection_once；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 OpenAsync、PauseNextClose、Run、DisposeAsync、WaitAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Concurrent_dispose_calls_release_the_serial_connection_once()
    {
        var connection = new LoopbackSerialConnection();
        var transport = new SerialByteStreamTransport(
            new SerialTransportOptions("COM_TEST", 9600),
            connection);
        await transport.OpenAsync();
        connection.PauseNextClose();

        var firstDispose = Task.Run(async () => await transport.DisposeAsync());
        await connection.WaitForCloseStartedAsync().WaitAsync(TimeSpan.FromSeconds(1));
        var secondCallReturned = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondDispose = Task.Run(async () =>
        {
            var operation = transport.DisposeAsync();
            secondCallReturned.TrySetResult();
            await operation;
        });

        await secondCallReturned.Task.WaitAsync(TimeSpan.FromSeconds(1));
        connection.ResumeClose();
        await Task.WhenAll(firstDispose, secondDispose);

        Assert.Equal(1, connection.CloseCount);
        Assert.Equal(1, connection.DisposeCount);
        Assert.False(transport.IsOpen);
    }

    /// <summary>
    /// 测试相关入口 Open_started_during_dispose_is_rejected_without_reopening_the_port；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 OpenAsync、PauseNextClose、Run、DisposeAsync、WaitAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Open_started_during_dispose_is_rejected_without_reopening_the_port()
    {
        var connection = new LoopbackSerialConnection();
        var transport = new SerialByteStreamTransport(
            new SerialTransportOptions("COM_TEST", 9600),
            connection);
        await transport.OpenAsync();
        connection.PauseNextClose();

        var disposeTask = Task.Run(async () => await transport.DisposeAsync());
        await connection.WaitForCloseStartedAsync().WaitAsync(TimeSpan.FromSeconds(1));
        var reopenTask = transport.OpenAsync().AsTask();
        connection.ResumeClose();

        await disposeTask;
        await Assert.ThrowsAsync<ObjectDisposedException>(() => reopenTask);
        Assert.Equal(1, connection.OpenCount);
        Assert.False(transport.IsOpen);
    }

    /// <summary>
    /// 测试相关入口 Pending_read_observes_caller_cancellation；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 OpenAsync、AsTask、ReadAsync、Cancel、ThrowsAnyAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Pending_read_observes_caller_cancellation()
    {
        var connection = new LoopbackSerialConnection();
        await using var transport = new SerialByteStreamTransport(
            new SerialTransportOptions("COM_TEST", 9600),
            connection);
        await transport.OpenAsync();
        using var cancellation = new CancellationTokenSource();

        // 测试串口不写入数据，挂起读取应由同一个调用方 token 终止。
        var readTask = transport.Input.ReadAsync(cancellation.Token).AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readTask);
    }

    /// <summary>
    /// LoopbackSerialConnection 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ISerialPortConnection。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class LoopbackSerialConnection : ISerialPortConnection
    {
        private readonly LoopbackStream _stream = new();
        private readonly TaskCompletionSource _closeStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _resumeClose = new(initialState: false);
        private bool _pauseNextClose;

        public bool IsOpen { get; private set; }
        public Stream Stream => _stream;
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public int DisposeCount { get; private set; }

        /// <summary>
        /// 测试相关入口 Open；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Open()
        {
            OpenCount++;
            IsOpen = true;
        }

        /// <summary>
        /// 测试相关入口 Close；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 TrySetResult、Wait；具体条件和先后次序见方法体。
        /// </summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            if (_pauseNextClose)
            {
                _pauseNextClose = false;
                _closeStarted.TrySetResult();
                _resumeClose.Wait();
            }

            CloseCount++;
            IsOpen = false;
        }

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Close、Dispose；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose()
        {
            DisposeCount++;
            Close();
            _stream.Dispose();
            _resumeClose.Dispose();
        }

        /// <summary>
        /// 测试相关入口 PauseNextClose；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void PauseNextClose() => _pauseNextClose = true;

        /// <summary>
        /// 测试相关入口 WaitForCloseStartedAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public Task WaitForCloseStartedAsync() => _closeStarted.Task;

        /// <summary>
        /// 测试相关入口 ResumeClose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Set；具体条件和先后次序见方法体。
        /// </summary>
        public void ResumeClose() => _resumeClose.Set();
    }

    /// <summary>
    /// LoopbackStream 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：Stream。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class LoopbackStream : Stream
    {
        private readonly Pipe _pipe = new();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// 测试相关入口 ReadAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ReadCoreAsync；具体条件和先后次序见方法体。
        /// </summary>
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ReadCoreAsync(buffer, cancellationToken);

        /// <summary>
        /// 测试相关入口 WriteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WriteAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await _pipe.Writer.WriteAsync(buffer, cancellationToken);
        }

        /// <summary>
        /// 测试相关入口 ReadCoreAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ReadAsync、Min、CopyTo、Slice、AdvanceTo；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        private async ValueTask<int> ReadCoreAsync(
            Memory<byte> destination,
            CancellationToken cancellationToken)
        {
            var result = await _pipe.Reader.ReadAsync(cancellationToken);
            var length = (int)Math.Min(result.Buffer.Length, destination.Length);
            result.Buffer.Slice(0, length).CopyTo(destination.Span);
            _pipe.Reader.AdvanceTo(result.Buffer.GetPosition(length));
            return length;
        }

        /// <summary>
        /// 测试相关入口 Flush；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override void Flush() { }
        /// <summary>
        /// 测试相关入口 FlushAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        /// <summary>
        /// 测试相关入口 Read；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 Write；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 Seek；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 SetLength；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Complete、Dispose；具体条件和先后次序见方法体。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pipe.Reader.Complete();
                _pipe.Writer.Complete();
            }
            base.Dispose(disposing);
        }
    }
}
