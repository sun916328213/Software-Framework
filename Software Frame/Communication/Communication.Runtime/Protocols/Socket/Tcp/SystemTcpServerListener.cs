// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：SystemTcpServerListener。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Socket、System.Net、System.Net.Sockets。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Socket;
using System.Net;
using System.Net.Sockets;

namespace Communication.Runtime.Protocols.Socket;

/// <summary>把系统 <see cref="TcpListener"/> 适配为项目内部监听器边界。</summary>
internal sealed class SystemTcpServerListener : ITcpServerListener
{
    private readonly TcpListener _listener;

    /// <summary>创建绑定到指定地址和端口的监听器。</summary>
    public SystemTcpServerListener(IPAddress localAddress, int port)
    {
        ArgumentNullException.ThrowIfNull(localAddress);
        _listener = new TcpListener(localAddress, port);
    }

    /// <inheritdoc />
    public bool IsListening { get; private set; }

    /// <inheritdoc />
    public int LocalPort => _listener.LocalEndpoint is IPEndPoint ep ? ep.Port : 0;

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Start。
    /// 正文中的关键协作调用包括 Start；具体条件和先后次序见方法体。
    /// </remarks>
    public void Start(int backlog)
    {
        _listener.Start(backlog);
        IsListening = true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Stop。
    /// 正文中的关键协作调用包括 Stop；具体条件和先后次序见方法体。
    /// </remarks>
    public void Stop()
    {
        if (!IsListening)
        {
            return;
        }

        IsListening = false;
        _listener.Stop();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 AcceptTcpClientAsync。
    /// 正文中的关键协作调用包括 AcceptTcpClientAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public ValueTask<TcpClient> AcceptTcpClientAsync(CancellationToken cancellationToken) =>
        _listener.AcceptTcpClientAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Stop；具体条件和先后次序见方法体。
    /// </remarks>
    public void Dispose() => Stop();
}
