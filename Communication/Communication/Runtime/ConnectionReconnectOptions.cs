namespace CommunicationNew.Runtime;

/// <summary>配置连接意外中断后的后台恢复策略。</summary>
public sealed class ConnectionReconnectOptions
{
    /// <summary>创建固定间隔的重连策略。</summary>
    /// <param name="delay">每次重连尝试前的等待时间；可为零。</param>
    public ConnectionReconnectOptions(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }

        Delay = delay;
    }

    /// <summary>每次重连尝试前的等待时间。</summary>
    public TimeSpan Delay { get; }
}
