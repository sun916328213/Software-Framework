using Prism.Ioc;
using SecsGem.Runtime;

namespace Secs;

/// <summary>兼容旧调用方；新代码使用 SecsGemRuntime。</summary>
[Obsolete("Use SecsGemRuntime instead.", false)]
public class SecsControl : SecsGemRuntime
{
    public SecsControl(IContainerProvider container) : base(container)
    {
    }
}
