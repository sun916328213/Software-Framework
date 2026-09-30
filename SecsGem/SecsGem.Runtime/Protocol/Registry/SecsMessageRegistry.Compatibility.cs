using Prism.Ioc;

namespace SemiTool.Tool;

/// <summary>Compatibility name for callers that used the old Tool namespace.</summary>
[Obsolete("Use SecsGem.Runtime.Protocol.Registry.SecsMessageRegistry instead.", false)]
public class SecsMessageRegistry : SecsGem.Runtime.Protocol.Registry.SecsMessageRegistry
{
    public SecsMessageRegistry(IContainerProvider containerProvider) : base(containerProvider)
    {
    }
}
