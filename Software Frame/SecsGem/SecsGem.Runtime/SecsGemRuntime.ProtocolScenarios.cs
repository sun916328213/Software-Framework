using SecsGem.Runtime.Protocol;

namespace SecsGem.Runtime;

public partial class SecsGemRuntime
{
    private readonly SecsProtocolScenarios _protocolScenarios;

    /// <summary>宿主显式启用超长消息与配方传输会话策略；仅在断线时配置。</summary>
    public void ConfigureProtocolScenarios(SecsProtocolScenarioOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _protocolScenarios.Configure(options);
    }

    internal Task DrainProtocolScenariosAsync() => _protocolScenarios.DrainAsync();
}
