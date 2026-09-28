using Secs4Net;

namespace Secs.Order.S10;

/// <summary>处理 S10F3/S10F5 终端消息，并生成终端确认应答。</summary>
internal static class S10TerminalMessageProcessor
{
    private static readonly SecsOrderBase ItemReader = new();

    public static Item? Process(string command, Item? terminalText, ITerminalDisplay display)
    {
        try
        {
            if (terminalText is not null)
            {
                var lines = ItemReader.SecsItemToList(terminalText);
                var message = string.Join("/r/n", lines);
                if (lines is not null)
                    display.Show(command, message);
            }

            return Item.B(0);
        }
        catch
        {
            // 保留原有协议行为：解析或显示失败交给调用方触发 S9F7。
            return null;
        }
    }
}
