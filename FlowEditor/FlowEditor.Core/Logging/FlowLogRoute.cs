using System.Security.Cryptography;
using System.Text;

namespace FlowEditor.Core;

/// <summary>
/// 流程日志的逻辑身份值对象；以独立字段参与相等比较，避免使用分隔字符串造成路由碰撞。
/// </summary>
public readonly record struct FlowLogRoute
{
    /// <summary>创建具有非空任务名称的流程日志路由。</summary>
    /// <param name="processName">可选的流程容器名称。</param>
    /// <param name="flowName">流程标签页或任务名称。</param>
    public FlowLogRoute(string? processName, string flowName)
    {
        if (string.IsNullOrEmpty(flowName))
            throw new ArgumentException("流程日志路由的任务名称不能为空。", nameof(flowName));

        ProcessName = string.IsNullOrEmpty(processName) ? null : processName;
        FlowName = flowName;
    }

    /// <summary>流程容器名称。</summary>
    public string? ProcessName { get; }

    /// <summary>流程标签页或任务名称。</summary>
    public string FlowName { get; }

    /// <summary>供界面和兼容查询使用的可读“流程容器\任务”键。</summary>
    public string DisplayKey => string.IsNullOrEmpty(ProcessName)
        ? FlowName
        : $"{ProcessName}\\{FlowName}";

    /// <summary>可读键是否因名称本身包含目录分隔符而无法唯一反解。</summary>
    public bool HasAmbiguousDisplayKey => ContainsDirectorySeparator(ProcessName)
        || ContainsDirectorySeparator(FlowName);

    /// <summary>供只接受字符串键的宿主 adapter 使用的稳定身份摘要。</summary>
    public string StableId
    {
        get
        {
            var process = ProcessName ?? string.Empty;
            var identity = $"{process.Length}:{process};{FlowName.Length}:{FlowName};";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
            return Convert.ToHexString(hash.AsSpan(0, 6));
        }
    }

    /// <summary>仅在任务名称非空时创建路由。</summary>
    public static bool TryCreate(string? processName, string? flowName, out FlowLogRoute route)
    {
        if (string.IsNullOrEmpty(flowName))
        {
            route = default;
            return false;
        }

        route = new FlowLogRoute(processName, flowName);
        return true;
    }

    /// <summary>判断名称是否包含 Windows 或 Unix 目录分隔符。</summary>
    private static bool ContainsDirectorySeparator(string? value)
        => value is not null && value.AsSpan().IndexOfAny('/', '\\') >= 0;
}
