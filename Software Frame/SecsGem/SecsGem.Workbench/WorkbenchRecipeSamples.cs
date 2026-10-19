using SecsGem.Workbench.Models;

namespace SecsGem.Workbench;

/// <summary>只定义本地示例，不序列化、不访问 Runtime，也不生成协议报文。</summary>
public static class WorkbenchRecipeSamples
{
    public static Dictionary<string, WorkbenchRecipe> Create() => new(StringComparer.Ordinal)
    {
        ["DEMO-FAST"] = new(1, 5000, 1.5, 100),
        ["DEMO-PRECISE"] = new(1, 10000, 2, 50)
    };
}
