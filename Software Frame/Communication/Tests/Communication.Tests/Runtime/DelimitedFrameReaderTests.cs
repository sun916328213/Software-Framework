// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DelimitedFrameReaderTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Frames、Communication.Runtime、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Frames;
using Communication.Runtime;
using Xunit;

namespace Communication.Tests.Runtime;

/// <summary>
/// T15：锁定「分包结束符」的切分是**字节级**且**有界**的。
/// </summary>
/// <remarks>
/// 改造前 TcpClientDevice / SerialPortDevice 各自在设备内部做
/// 「字节 → Encoding.GetString → IndexOf(结束符) → 截字符串 → Encoding.GetBytes → 字节」的往返。
/// 这个往返在两类输入上不是恒等变换：① 跨传输块被切开的多字节字符，第一段解码成 U+FFFD，
/// 原始字节**永久丢失**；② 含非法 UTF-8 序列的二进制载荷被静默改写。
/// 另外待拼缓冲是一个没有任何上限的 StringBuilder，对端不发结束符就能把内存撑满。
/// 本文件的用例逐条钉住这两件事。
/// </remarks>
public sealed class DelimitedFrameReaderTests
{
    private static readonly byte[] CrLf = [0x0D, 0x0A];

    /// <summary>按给定分块顺序喂给读取器，返回产出的全部帧。</summary>
    private static List<byte[]> Split(params byte[][] chunks)
    {
        var reader = DelimitedFrameReader.ForDelimiter(CrLf);
        var frames = new List<byte[]>();
        foreach (var chunk in chunks)
        {
            reader.Append(chunk, frames.Add);
        }

        return frames;
    }

    [Fact]
    public void 结束符本身不进入帧内容()
    {
        var frames = Split([0x41, 0x42, 0x0D, 0x0A]);

        Assert.Equal(new byte[] { 0x41, 0x42 }, Assert.Single(frames));
    }

    [Fact]
    public void 一段字节里的多个帧按出现顺序交付()
    {
        var frames = Split([0x41, 0x0D, 0x0A, 0x42, 0x43, 0x0D, 0x0A, 0x44]);

        Assert.Equal(2, frames.Count);
        Assert.Equal(new byte[] { 0x41 }, frames[0]);
        Assert.Equal(new byte[] { 0x42, 0x43 }, frames[1]);
    }

    [Fact]
    public void 结束符被传输块切开时仍能拼出帧()
    {
        var frames = Split([0x41, 0x0D], [0x0A, 0x42, 0x0D, 0x0A]);

        Assert.Equal(2, frames.Count);
        Assert.Equal(new byte[] { 0x41 }, frames[0]);
        Assert.Equal(new byte[] { 0x42 }, frames[1]);
    }

    [Fact]
    public void 不构成结束符的半个前缀必须留在帧里()
    {
        // 第二段里的 0x0D 后面跟的不是 0x0A，所以它是载荷，不是结束符的一半。
        var frames = Split([0x41, 0x0D], [0x42, 0x0D, 0x0A]);

        Assert.Equal(new byte[] { 0x41, 0x0D, 0x42 }, Assert.Single(frames));
    }

    [Fact]
    public void 多字节字符被传输块切开时原始字节必须原样保留()
    {
        // 「中」= E4 B8 AD，结束符是 CRLF。改造前的文本往返会把它变成两个 U+FFFD。
        var frames = Split([0xE4, 0xB8], [0xAD, 0x0D, 0x0A]);

        Assert.Equal(new byte[] { 0xE4, 0xB8, 0xAD }, Assert.Single(frames));
    }

    [Fact]
    public void 二进制载荷不得被改写()
    {
        // 0xFF 0xFE 不是合法 UTF-8；改造前的文本往返会把它换成 EF BF BD EF BF BD。
        var frames = Split([0xFF, 0xFE, 0x00, 0x01, 0x0D, 0x0A]);

        Assert.Equal(new byte[] { 0xFF, 0xFE, 0x00, 0x01 }, Assert.Single(frames));
    }

    [Fact]
    public void 未凑满的部分留在待拼缓冲且不产生帧()
    {
        var reader = DelimitedFrameReader.ForDelimiter(CrLf);
        var frames = new List<byte[]>();

        reader.Append([0x41, 0x42, 0x0D], frames.Add);

        Assert.Empty(frames);
        Assert.Equal(3, reader.PendingCount);

        reader.Append([0x0A], frames.Add);

        Assert.Equal(new byte[] { 0x41, 0x42 }, Assert.Single(frames));
        Assert.Equal(0, reader.PendingCount);
    }

    [Fact]
    public void 空字节块既不产生帧也不改变待拼状态()
    {
        var reader = DelimitedFrameReader.ForDelimiter(CrLf);
        reader.Append([0x41], _ => { });

        reader.Append([], _ => throw new InvalidOperationException("空字节块不应产出帧。"));

        Assert.Equal(1, reader.PendingCount);
    }

    [Fact]
    public void 对端一直不发结束符时必须报错而不是无限增长()
    {
        var reader = DelimitedFrameReader.ForDelimiter(CrLf);
        var chunk = new byte[8192];

        var exception = Assert.Throws<InvalidDataException>(() =>
        {
            for (var i = 0; i < 16; i++)
            {
                reader.Append(chunk, _ => { });
            }
        });

        Assert.Contains("结束符", exception.Message);
        // 上限来自分帧器的 maxFrame(65536)：越界那一刻就拒绝，不会先把内存撑满再报错。
        Assert.True(
            reader.PendingCount <= 65536 + chunk.Length,
            $"待拼量 {reader.PendingCount} 超出了「上限 + 一个传输块」的容许范围。");
    }

    [Fact]
    public void 构造参数缺失时必须立即拒绝()
    {
        Assert.Throws<ArgumentNullException>(() => new DelimitedFrameReader(null!));
        Assert.Throws<ArgumentNullException>(() => DelimitedFrameReader.ForDelimiter(null!));

        var reader = DelimitedFrameReader.ForDelimiter(CrLf);
        Assert.Throws<ArgumentNullException>(() => reader.Append([0x41], null!));
    }

    [Fact]
    public void 任意分帧器都能接入同一个读取器()
    {
        // 长度前缀分帧（前 2 字节大端长度 + 载荷）：证明这个 seam 不绑定在「结束符」这一种策略上。
        var reader = new DelimitedFrameReader(new LengthPrefixedFrameSplitter());
        var frames = new List<byte[]>();

        reader.Append([0x00, 0x02, 0x41], frames.Add);
        Assert.Empty(frames);

        reader.Append([0x42, 0x00, 0x01, 0x43], frames.Add);

        Assert.Equal(2, frames.Count);
        Assert.Equal(new byte[] { 0x00, 0x02, 0x41, 0x42 }, frames[0]);
        Assert.Equal(new byte[] { 0x00, 0x01, 0x43 }, frames[1]);
    }
}
