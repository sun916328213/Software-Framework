using CommunicationNew.Protocols.Mitsubishi;
using Xunit;

namespace CommunicationNew.Tests.Protocols.Mitsubishi;

public sealed class MxComponentWordCodecTests
{
    [Fact]
    public void Mx_words_are_encoded_as_explicit_little_endian_mc_payload()
    {
        var words = new short[] { 0x1234, unchecked((short)0xFEDC) };

        Assert.Equal(
            new byte[] { 0x34, 0x12, 0xDC, 0xFE },
            MxComponentWordCodec.ToPayload(words));
    }

    [Fact]
    public void Mc_payload_is_decoded_to_signed_mx_words_without_using_host_endianness()
    {
        var payload = new byte[] { 0x34, 0x12, 0xDC, 0xFE };

        Assert.Equal(
            new short[] { 0x1234, unchecked((short)0xFEDC) },
            MxComponentWordCodec.FromPayload(payload));
    }

    [Fact]
    public void Odd_length_word_payload_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            MxComponentWordCodec.FromPayload(new byte[] { 0x34 }));
    }
}
