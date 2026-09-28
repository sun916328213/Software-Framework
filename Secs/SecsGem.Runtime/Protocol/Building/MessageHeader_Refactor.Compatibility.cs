using Secs4Net;
using SecsGem.Runtime.Protocol.Building;

namespace Secs.Tool;

/// <summary>Compatibility name for the previous S9 header builder.</summary>
[Obsolete("Use HsmsMessageHeaderBuilder within Runtime instead.", false)]
public class MessageHeader_Refactor
{
    internal static byte[] GetHeaderInfo(SecsMessage message, ushort deviceId, int id) =>
        HsmsMessageHeaderBuilder.GetHeaderInfo(message, deviceId, id);
}
