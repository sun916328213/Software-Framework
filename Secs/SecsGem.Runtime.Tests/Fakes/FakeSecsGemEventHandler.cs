using SecsGem.Core;

namespace SecsGem.Runtime.Tests.Fakes;

internal sealed class FakeSecsGemEventHandler(byte result = 0) : ISecsGemEventHandler
{
    public SecsGemRemoteCommand? LastCommand { get; private set; }

    public Task<byte> OnRemoteCommandAsync(SecsGemRemoteCommand command)
    {
        LastCommand = command;
        return Task.FromResult(result);
    }
}
