// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsProtocolEngine。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Runtime、Extension.Core.Concurrency、SecsGem.Runtime.Order.S9、Secs4Net、SecsGem.Runtime.DataBinding、SecsGem.Runtime.Transport。导入不代表所有类型都会在运行时加载。
using SecsGem.Runtime;
using Extension.Core.Concurrency;
using SecsGem.Runtime.Order.S9;
using Secs4Net;
using SecsGem.Runtime.DataBinding;
using SecsGem.Runtime.Transport;
using SecsGem.Runtime.Protocol.Registry;

namespace SecsGem.Runtime.Protocol;

/// <summary>将 SxFy 处理器编排与 HSMS 收发分开。</summary>
internal sealed class SecsProtocolEngine
{
    private readonly SecsMessageRegistry _registry;
    private readonly SecsDataBinding _binding;
    private readonly ISecsTransport _transport;
    private readonly Func<string, Item, bool, Task<SecsMessage>> _send;
    private readonly Func<SecsMessage, Item, Task> _reply;
    private readonly Action<string> _error;

    /// <summary>
    /// 初始化 SecsProtocolEngine 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    internal SecsProtocolEngine(
        SecsMessageRegistry registry,
        SecsDataBinding binding,
        ISecsTransport transport,
        Func<string, Item, bool, Task<SecsMessage>> send,
        Func<SecsMessage, Item, Task> reply,
        Action<string> error = null)
    {
        _registry = registry;
        _binding = binding;
        _transport = transport;
        _send = send;
        _reply = reply;
        _error = error;
    }

    /// <summary>
    /// 处理接收操作；对应入口为 ReceiveAsync。
    /// 正文中的关键协作调用包括 Refresh、GetHandler、ConfigureAwait、GetReplyItemAsync、_reply；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    internal async Task<Item?> ReceiveAsync(SecsMessage message)
    {
        if (message == null) return null;

        var order = $"S{message.S}F{message.F}";
        if (order is "S1F3" or "S1F11")
        {
            _binding.Refresh();
        }

        var handler = _registry.GetHandler(order)
            ?? throw new InvalidOperationException($"未找到对应的处理器: {order}");
        var item = await handler.GetReplyItemAsync(message).ConfigureAwait(false);

        // 回包失败绝不能吞掉事件。
        // Host 的主消息没带 W 位时，secs4net 的 TryReplyAsync 直接抛
        // SecsException("The message does not need to reply")（PrimaryMessageWrapper.cs:27）；
        // 重复回包时它返回 false，Secs4NetTransport.ReplyAsync 转成 InvalidOperationException。
        // 原来这两条都直接冲出去，后面的 TriggerEvent 永远执行不到 ——
        // 现象是 Host 明确发了 S1F17/S1F15，ConnectManager 的监听却一次都没跑。
        // 事件表达的是"Host 发来了这条请求"，跟回没回上包是两件事。
        try
        {
            await _reply(message, item).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _error?.Invoke($"{order} 回包未发出（不影响该消息的后续处理）: {ex.Message}");
        }

        // 把回包 item 一并带出：监听方（如 ConnectManager.Online_HtoE）需要区分
        // "收到请求"与"接受请求"——ONLACK=2(NOT_READY) 被拒时不得切控制状态。
        EventHelper.TriggerEvent(order, item);
        return item;
    }

    /// <summary>
    /// 执行发送操作；对应入口为 SendItemAsync。
    /// 正文中的关键协作调用包括 IsNullOrEmpty、GetHandler、GetSendItem、ConfigureAwait、_send；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    internal async Task SendItemAsync(string order, bool wait, params object[] data)
    {
        if (string.IsNullOrEmpty(order)) return;

        var handler = _registry.GetHandler(order)
            ?? throw new InvalidOperationException($"未找到对应的处理器: {order}");
        Item? item;
        if (order == "S6F11")
        {
            // S6F11 处理器约定两个参数：事件数据与 VIDManager。
            item = data.Length > 0
                ? handler.GetSendItem(new object[] { data[0], _binding.Manager })
                : null;
        }
        else
        {
            item = handler.GetSendItem(data);
        }

        if (item == null)
        {
            throw new InvalidOperationException($"{order} 组包失败，已取消发送");
        }

        try
        {
            var reply = await _send(order, item, wait).ConfigureAwait(false);

            // 修复：传输层在会话未建立（没 Selected）或连接已断开时返回 null 而不是抛异常
            //（Secs4NetTransport.Send 的三处 early-return）。原来这里不检查，S6F11 发出去
            // 没有任何回音也算"成功" —— 现象是"发送正常"，实际 Host 什么都没收到；
            // 而标准 Host 在 Selected 之前本来就不会发任何数据，于是收发两边一起"正常"地没动静。
            // wait=false 的即发即忘保持原语义，只对明确要求等应答的负责。
            if (wait && reply == null)
                throw new InvalidOperationException(
                    $"{order} 没有收到应答：会话未建立（HSMS 未 Selected）或连接已断开");
        }
        catch (SecsException ex)
        {
            if (ex.Message.Contains("T3 Timeout", StringComparison.Ordinal))
            {
                var timeout = new SS9F9
                {
                    SecsMessage = ex.SecsMessage,
                    DeviceID = _transport.DeviceID,
                    ID = _transport.ID
                };
                await SendItemAsync("S9F9", false, timeout).ConfigureAwait(false);
                throw new Exception($"T3超时: {ex.Message}", ex);
            }

            throw new Exception($"Secs异常: {ex.Message}", ex);
        }
    }
}
