// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IConnection。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Abstractions;

/// <summary>连接生命周期的最小能力接口。</summary>
public interface IConnection : IAsyncDisposable
{
    /// <summary>当前生命周期状态。</summary>
    ConnectionState State { get; }

    /// <summary>建立连接；重复调用已连接实例不会创建第二条连接。</summary>
    ValueTask ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>取消活动操作并等待连接关闭。</summary>
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);
}
