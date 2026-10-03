// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ITcpServerListener。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Socket、System.Net.Sockets。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Socket;
using System.Net.Sockets;

namespace Communication.Runtime.Protocols.Socket;

/// <summary>TCP 服务端监听器的内部边界，用于明确监听句柄与 accept 循环的所有权。</summary>
internal interface ITcpServerListener : IDisposable
{
    /// <summary>监听器是否处于启动状态。</summary>
    bool IsListening { get; }

    /// <summary>实际监听的端口号（Port=0 时由系统分配，Start 后可读回）。</summary>
    int LocalPort { get; }

    /// <summary>按指定连接积压上限启动监听。</summary>
    void Start(int backlog);

    /// <summary>停止监听并解除正在等待的 accept。</summary>
    void Stop();

    /// <summary>异步接受一个 TCP 客户端。</summary>
    ValueTask<TcpClient> AcceptTcpClientAsync(CancellationToken cancellationToken);
}
