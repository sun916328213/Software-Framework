using Secs;

namespace SecsGem.Runtime.Protocol.Messages.S10;

/// <summary>Forwards terminal text to the runtime without choosing a UI technology.</summary>
internal sealed class TerminalMessageEventDisplay(Action<string, string> callback) : ITerminalDisplay
{
    public void Show(string title, string message) => callback(title, message);
}
