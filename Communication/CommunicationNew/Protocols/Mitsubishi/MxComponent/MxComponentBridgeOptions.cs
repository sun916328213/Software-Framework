namespace CommunicationNew.Protocols.Mitsubishi;

/// <summary>MX Component 独立桥接进程的启动参数。</summary>
public sealed class MxComponentBridgeOptions
{
    /// <summary>.NET Framework 4.8 桥接程序的绝对路径。</summary>
    public required string ExecutablePath { get; init; }

    /// <summary>由 MX Component Communication Setup Utility 配置的逻辑站号，范围为 0 到 1023。</summary>
    public int LogicalStationNumber { get; init; }

    /// <summary>逻辑站访问密码；通过本机命名管道发送，不写入桥接进程命令行。</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>等待桥接进程建立本机命名管道的最长时间。</summary>
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>单次 MX 读、写或关闭命令的最长等待时间。</summary>
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(3);
}
