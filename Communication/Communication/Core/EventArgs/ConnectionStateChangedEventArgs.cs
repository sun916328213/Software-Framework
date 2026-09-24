namespace CommunicationNew.Core.EventArgs
{
    /// <summary>
    /// 连接状态变化事件参数
    /// </summary>
    public class ConnectionStateChangedEventArgs : System.EventArgs
    {
        /// <summary>是否已连接</summary>
        public bool IsConnected { get; }

        /// <summary>设备名称</summary>
        public string DeviceName { get; }

        /// <summary>变化时间</summary>
        public DateTime Timestamp { get; } = DateTime.Now;

        /// <summary>附加消息</summary>
        public string? Message { get; }

        public ConnectionStateChangedEventArgs(bool isConnected, string deviceName, string? message = null)
        {
            IsConnected = isConnected;
            DeviceName = deviceName;
            Message = message;
        }
    }
}
