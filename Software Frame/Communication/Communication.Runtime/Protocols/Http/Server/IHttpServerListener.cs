// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IHttpServerListener。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Http、System.Net。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Http;
using System.Net;

namespace Communication.Runtime.Protocols.Http;

/// <summary>
/// HTTP 服务端监听器的内部边界，用于隔离操作系统 URL ACL 与真实网络句柄。
/// </summary>
internal interface IHttpServerListener : IDisposable
{
    /// <summary>监听器是否正在接收请求。</summary>
    bool IsListening { get; }

    /// <summary>添加要监听的 URI 前缀。</summary>
    void AddPrefix(string prefix);

    /// <summary>启动监听。</summary>
    void Start();

    /// <summary>停止监听并解除正在等待的请求。</summary>
    void Stop();

    /// <summary>异步等待下一个 HTTP 上下文。</summary>
    Task<HttpListenerContext> GetContextAsync();
}
