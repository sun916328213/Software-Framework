// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ConnectionReconnectOptions。成员说明可结合下方具体实现阅读。
namespace Communication.Runtime;

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
