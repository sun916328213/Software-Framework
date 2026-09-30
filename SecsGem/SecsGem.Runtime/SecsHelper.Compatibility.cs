using Prism.Ioc;
using SecsGem.Runtime.Transport;

namespace Secs;

/// <summary>旧传输入口；新代码经 SecsGemRuntime 使用组合的传输适配器。</summary>
[Obsolete("Use SecsGem.Runtime.SecsGemRuntime instead.", false)]
public class SecsHelper : Secs4NetTransport
{
    public SecsHelper(IContainerProvider container) : base(container)
    {
    }
}
