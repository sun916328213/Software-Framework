namespace Secs;

/// <summary>向设备操作界面显示 Host 发送的终端消息。</summary>
public interface ITerminalDisplay
{
    void Show(string title, string message);
}
