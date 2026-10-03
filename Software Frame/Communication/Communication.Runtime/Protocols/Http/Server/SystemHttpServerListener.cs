// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：SystemHttpServerListener。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Http、System.Net。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Http;
using System.Net;

namespace Communication.Runtime.Protocols.Http;

/// <summary>把系统 <see cref="HttpListener"/> 适配为项目内部监听器边界。</summary>
internal sealed class SystemHttpServerListener : IHttpServerListener
{
    private readonly HttpListener _listener = new();

    /// <inheritdoc />
    public bool IsListening => _listener.IsListening;

    /// <inheritdoc />
    /// <remarks>
    /// 执行添加操作；对应入口为 AddPrefix。
    /// 正文中的关键协作调用包括 Contains、Add；具体条件和先后次序见方法体。
    /// </remarks>
    public void AddPrefix(string prefix)
    {
        // 重连会再次配置同一地址；保持幂等可避免 Prefixes.Add 抛出重复项异常。
        if (!_listener.Prefixes.Contains(prefix))
        {
            _listener.Prefixes.Add(prefix);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Start。
    /// 正文中的关键协作调用包括 Start；具体条件和先后次序见方法体。
    /// </remarks>
    public void Start() => _listener.Start();

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Stop。
    /// 正文中的关键协作调用包括 Stop；具体条件和先后次序见方法体。
    /// </remarks>
    public void Stop()
    {
        if (_listener.IsListening)
        {
            _listener.Stop();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 获取当前实现提供的值或对象；对应入口为 GetContextAsync。
    /// 正文中的关键协作调用包括 GetContextAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public Task<HttpListenerContext> GetContextAsync() => _listener.GetContextAsync();

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
    /// </remarks>
    public void Dispose() => _listener.Close();
}
