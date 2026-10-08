using Secs4Net;
using SecsGem.Runtime.Protocol.Building;
using Xunit;

namespace SecsGem.Tests.Runtime;

public class HsmsMessageHeaderBuilderTests
{
    [Theory]
    [InlineData(false, 0x01)]
    [InlineData(true, 0x81)]
    public void Header_PreservesWireLayoutAndReplyBit(bool replyExpected, byte stream)
    {
        using var message = new SecsMessage(1, 9, replyExpected);

        var header = HsmsMessageHeaderBuilder.GetHeaderInfo(message, 0xFEDC, unchecked((int)0x89ABCDEF));

        Assert.Equal(new byte[] { 0xFE, 0xDC, stream, 9, 0, 0, 0x89, 0xAB, 0xCD, 0xEF }, header);
    }
}
