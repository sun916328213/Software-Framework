using Extension.Core.Concurrency;
using Secs.Order.S9;
using Secs4Net;
using SecsGem.Runtime.DataBinding;
using SecsGem.Runtime.Transport;
using SemiTool.Tool;

namespace SecsGem.Runtime.Protocol;

/// <summary>将 SxFy 处理器编排与 HSMS 收发分开。</summary>
internal sealed class SecsProtocolEngine
{
    private readonly SecsMessageRegistry _registry;
    private readonly SecsDataBinding _binding;
    private readonly ISecsTransport _transport;
    private readonly Func<string, Item, bool, Task<SecsMessage>> _send;
    private readonly Func<SecsMessage, Item, Task> _reply;

    internal SecsProtocolEngine(
        SecsMessageRegistry registry,
        SecsDataBinding binding,
        ISecsTransport transport,
        Func<string, Item, bool, Task<SecsMessage>> send,
        Func<SecsMessage, Item, Task> reply)
    {
        _registry = registry;
        _binding = binding;
        _transport = transport;
        _send = send;
        _reply = reply;
    }

    internal async Task ReceiveAsync(SecsMessage message)
    {
        if (message == null) return;

        var order = $"S{message.S}F{message.F}";
        if (order is "S1F3" or "S1F11")
        {
            _binding.Refresh();
        }

        var handler = _registry.GetHandler(order)
            ?? throw new InvalidOperationException($"未找到对应的处理器: {order}");
        var item = await handler.GetReplyItemAsync(message).ConfigureAwait(false);
        await _reply(message, item).ConfigureAwait(false);
        EventHelper.TriggerEvent(order);
    }

    internal async Task SendItemAsync(string order, bool wait, params object[] data)
    {
        if (string.IsNullOrEmpty(order)) return;

        var handler = _registry.GetHandler(order)
            ?? throw new InvalidOperationException($"未找到对应的处理器: {order}");
        var item = order == "S6F11"
            ? handler.GetSendItem(data, _binding.Manager)
            : handler.GetSendItem(data);
        if (item == null)
        {
            throw new InvalidOperationException($"{order} 组包失败，已取消发送");
        }

        try
        {
            await _send(order, item, wait).ConfigureAwait(false);
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
