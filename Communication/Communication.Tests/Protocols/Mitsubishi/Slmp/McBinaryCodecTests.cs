using System.Buffers;
using CommunicationNew.Protocols.Mitsubishi;
using Xunit;

namespace CommunicationNew.Tests.Protocols.Mitsubishi;

public sealed class McBinaryCodecTests
{
    [Fact]
    public void Three_e_word_read_matches_official_slmp_field_order()
    {
        var request = McRequest.Read(McAddressValue.Parse("D100"), quantity: 2);
        var codec = new McBinaryCodec(new McCodecOptions
        {
            FrameFormat = McFrameFormat.ThreeE,
            NetworkNumber = 0,
            StationNumber = 0xFF,
            ModuleIoNumber = 0x03FF,
            ModuleStationNumber = 0,
            MonitoringTimer = 0x0010,
        });

        var frame = codec.Encode(request, correlationId: 0x1234);

        Assert.Equal(new byte[]
        {
            0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x0C, 0x00, 0x10, 0x00,
            0x01, 0x04, 0x00, 0x00,
            0x64, 0x00, 0x00, 0xA8, 0x02, 0x00,
        }, frame.ToArray());
    }

    [Fact]
    public void Decoder_waits_for_complete_three_e_response_and_checks_end_code()
    {
        var codec = new McBinaryCodec(new McCodecOptions
        {
            FrameFormat = McFrameFormat.ThreeE,
            NetworkNumber = 0,
            StationNumber = 0xFF,
            ModuleIoNumber = 0x03FF,
        });
        var partial = new ReadOnlySequence<byte>(new byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06,
        });
        Assert.False(codec.TryDecode(partial, 0, out _, out var partialConsumed));
        Assert.Equal(partial.Start, partialConsumed);

        var complete = new ReadOnlySequence<byte>(new byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x06, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56,
        });
        Assert.True(codec.TryDecode(complete, 0, out var response, out var consumed));
        Assert.Equal(complete.End, consumed);
        Assert.Equal(new byte[] { 0x34, 0x12, 0x78, 0x56 }, response.Data.ToArray());

        var error = new ReadOnlySequence<byte>(new byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x02, 0x00, 0x59, 0xC0,
        });
        var exception = Assert.Throws<McProtocolException>(() =>
        {
            codec.TryDecode(error, 0, out _, out _);
        });
        Assert.Equal((ushort)0xC059, exception.EndCode);
    }

    [Fact]
    public void Address_parser_prefers_multi_letter_devices_and_uses_device_radix()
    {
        var specialRelay = McAddressValue.Parse("SM100");
        var linkRelay = McAddressValue.Parse("B10");

        Assert.Equal((byte)0x91, specialRelay.DeviceCode);
        Assert.Equal(100, specialRelay.Number);
        Assert.Equal((byte)0xA0, linkRelay.DeviceCode);
        Assert.Equal(0x10, linkRelay.Number);
    }

    [Fact]
    public void Four_e_frame_carries_and_validates_request_serial_number()
    {
        var codec = new McBinaryCodec(new McCodecOptions
        {
            FrameFormat = McFrameFormat.FourE,
            NetworkNumber = 0,
            StationNumber = 0xFF,
            ModuleIoNumber = 0x03FF,
        });
        var request = McRequest.Read(McAddressValue.Parse("D100"), 1);

        var encoded = codec.Encode(request, correlationId: 0x1234).ToArray();
        Assert.Equal(
            new byte[] { 0x54, 0x00, 0x34, 0x12, 0x00, 0x00 },
            encoded[..6]);

        var response = new ReadOnlySequence<byte>(new byte[]
        {
            0xD4, 0x00, 0x34, 0x12, 0x00, 0x00,
            0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x04, 0x00, 0x00, 0x00, 0x2A, 0x00,
        });
        Assert.True(codec.TryDecode(response, 0x1234, out var decoded, out _));
        Assert.Equal(new byte[] { 0x2A, 0x00 }, decoded.Data.ToArray());

        Assert.Throws<McProtocolException>(() =>
        {
            codec.TryDecode(response, 0x1235, out _, out _);
        });
    }
}
