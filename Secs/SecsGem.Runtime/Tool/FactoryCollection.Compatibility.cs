using Prism.Ioc;

namespace SemiTool.Tool;

/// <summary>兼容旧处理器注册；新代码使用 SecsMessageRegistry。</summary>
[Obsolete("Use SecsMessageRegistry instead.", false)]
public class FactoryCollection : SecsMessageRegistry
{
    public FactoryCollection(IContainerProvider containerProvider) : base(containerProvider)
    {
    }
}
