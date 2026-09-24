using CommunicationNew.Protocols.Mitsubishi;
using Xunit;

namespace CommunicationNew.Tests.Protocols.Mitsubishi;

public sealed class MxComponentBitCodecTests
{
    [Fact]
    public void Read_window_aligns_an_arbitrary_start_to_a_sixteen_point_word()
    {
        var window = MxComponentBitCodec.CreateReadWindow(start: 17, quantity: 17);

        Assert.Equal(16, window.AlignedStart);
        Assert.Equal(1, window.BitOffset);
        Assert.Equal(2, window.WordCount);
    }

    [Fact]
    public void Read_words_are_packed_using_the_mc_bit_nibble_format()
    {
        // 从字内 bit 1 开始依次读取 1、0、1、1。
        var words = new short[] { 0b0000_0000_0001_1010 };

        var payload = MxComponentBitCodec.PackReadWords(words, bitOffset: 1, quantity: 4);

        Assert.Equal(new byte[] { 0x10, 0x11 }, payload);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    public void Packed_write_payload_is_decoded_one_point_at_a_time(int index, short expected)
    {
        var payload = new byte[] { 0x10, 0x11 };

        Assert.Equal(expected, MxComponentBitCodec.GetWriteValue(payload, index));
    }
}
