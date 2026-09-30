using SecsGem.Runtime;

namespace Secs;

/// <summary>兼容旧 Prism 模块注册；新代码使用 SecsGemModule。</summary>
[Obsolete("Use SecsGemModule instead.", false)]
public class SecsToolModule : SecsGemModule
{
}
