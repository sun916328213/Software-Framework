using Vision.AI.Invi;
using Vision.AI.Aiditron;
using Vision.AI.Halcon;
using Vision.Core.AI;
using Xunit;

namespace Vision.Tests.Tools.AI;

[Trait("Category", "Contract")]
public sealed class P5DeviceSelectionTests
{
    [Theory]
    [InlineData("Halcon", "gpu0")]
    [InlineData("Aiditron", "cpu")]
    [InlineData("Invi", "gpu0")]
    public void UnsupportedSelection_ThrowsBeforeModelOrRuntimeAccess(string backend, string device)
    {
        IAIProvider provider = backend switch { "Halcon" => new HalconAIProvider(), "Aiditron" => new AiditronProvider(), _ => new InviProvider() };
        Assert.Throws<NotSupportedException>(() => provider.OpenSession(new AIModelRequest
        { ModelType = AIModelType.Detection, ModelPath = "not-opened.model", DeviceId = device }));
    }

    [Fact]
    public void UnsupportedDevice_IsRejectedBeforeCreatingInviNativeManager()
    {
        var calls = 0;
        var provider = new InviProvider(() => { calls++; throw new InvalidOperationException("fake factory must not be called"); });
        Assert.Throws<NotSupportedException>(() => provider.OpenSession(new AIModelRequest
        {
            ModelType = AIModelType.Detection, ModelPath = "contract.model", DeviceId = "gpu0"
        }));
        Assert.Equal(0, calls);
    }
}
