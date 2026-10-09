// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：DelimitedFrameReader。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Frames、Communication.Core.Interfaces。导入不代表所有类型都会在运行时加载。
using Communication.Core.Frames;
using Communication.Core.Interfaces;

namespace Communication.Runtime;

/// <summary>
/// 把连续字节块按「分包结束符」增量切成完整帧的读取器（T15）。
/// </summary>
/// <remarks>
/// <para>
/// 存在的理由有两个，都不是风格问题。
/// </para>
/// <para>
/// 一是 <b>字节级</b>。改造前 <c>TcpClientDevice</c> / <c>SerialPortDevice</c> 各自在设备里用
/// <c>Encoding.GetString</c> / <c>Encoding.GetBytes</c> 做「字节 → 文本 → 找结束符 → 文本 → 字节」的往返，
/// 于是：① 跨传输块被切开的多字节字符在解码时变成 U+FFFD，<b>原始字节永久丢失</b>
/// （<c>"中"</c> 被切成 <c>E4 B8</c> + <c>AD 0D 0A</c> 时，帧内容会变成 U+FFFD 而不是「中」）；
/// ② 二进制载荷（含非法 UTF-8 序列）被静默改写。
/// </para>
/// <para>
/// 二是 <b>有界</b>。改造前待拼缓冲是一个没有任何上限的 <see cref="System.Text.StringBuilder"/>，
/// 对端只要一直不发结束符，内存就能无限增长。这里直接复用 Core 已经提供、但全仓<b>无人使用</b>的
/// <see cref="DelimiterFrameSplitter"/>：它是字节级的，并且带 <c>maxFrame</c> 上限。
/// </para>
/// </remarks>
internal sealed class DelimitedFrameReader
{
    private readonly IFrameSplitter _splitter;
    private byte[] _buffer = new byte[256];
    private int _length;

    /// <summary>用任意分帧器构造（便于单测覆盖长度前缀 / 定长等其它策略）。</summary>
    public DelimitedFrameReader(IFrameSplitter splitter)
    {
        ArgumentNullException.ThrowIfNull(splitter);
        _splitter = splitter;
    }

    /// <summary>
    /// 按「分包结束符」构造。结束符本身<b>不</b>进入帧内容 —— 与改造前 <c>text[..index]</c> 的语义逐字一致。
    /// </summary>
    /// <param name="delimiter">结束符的字节表示；由调用方按设备编码把配置里的字符串转成字节。</param>
    public static DelimitedFrameReader ForDelimiter(byte[] delimiter)
    {
        ArgumentNullException.ThrowIfNull(delimiter);
        return new DelimitedFrameReader(new DelimiterFrameSplitter(delimiter, includeDelimiter: false));
    }

    /// <summary>当前待拼字节数（诊断与测试用）。</summary>
    public int PendingCount => _length;

    /// <summary>
    /// 追加一段接收字节，并对每个<b>已完整</b>的帧调用一次 <paramref name="onFrame"/>。
    /// 帧按出现顺序交付，结束符不进入帧；未凑满的部分留在缓冲里等下一段字节。
    /// </summary>
    /// <param name="data">本次从传输读到的一段连续字节。</param>
    /// <param name="onFrame">每产出一个完整帧时调用；入参是帧的独立副本，回调内可安全持有。</param>
    /// <exception cref="InvalidDataException">
    /// 待拼缓冲超过分帧器上限（对端一直没有发出结束符）。抛出而不是继续增长，
    /// 是为了让「对端协议不对」变成一个可诊断的连接故障，而不是一次 OOM。
    /// </exception>
    public void Append(ReadOnlySpan<byte> data, Action<byte[]> onFrame)
    {
        ArgumentNullException.ThrowIfNull(onFrame);

        if (!data.IsEmpty)
        {
            EnsureCapacity(data.Length);
            data.CopyTo(_buffer.AsSpan(_length));
            _length += data.Length;
        }

        while (_splitter.TrySplit(_buffer.AsSpan(0, _length), out var frame, out var consumed))
        {
            // frame 是分帧器内部 ToArray() 出来的独立副本，所以这里可以安全地就地压缩缓冲。
            if (consumed > 0)
            {
                _buffer.AsSpan(consumed, _length - consumed).CopyTo(_buffer);
                _length -= consumed;
            }

            onFrame(frame);
        }
    }

    private void EnsureCapacity(int extra)
    {
        var required = _length + extra;
        if (required <= _buffer.Length)
        {
            return;
        }

        var size = _buffer.Length;
        while (size < required)
        {
            size *= 2;
        }

        Array.Resize(ref _buffer, size);
    }
}
