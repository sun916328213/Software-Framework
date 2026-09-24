namespace CommunicationNew.Core.EventArgs
{
    /// <summary>
    /// 通信错误事件参数
    /// </summary>
    public class CommunicationErrorEventArgs : System.EventArgs
    {
        /// <summary>错误异常</summary>
        public Exception Exception { get; }

        /// <summary>设备名称</summary>
        public string DeviceName { get; }

        /// <summary>错误时间</summary>
        public DateTime Timestamp { get; } = DateTime.Now;

        /// <summary>错误消息</summary>
        public string Message => Exception?.Message ?? "未知错误";

        public CommunicationErrorEventArgs(Exception ex, string deviceName)
        {
            Exception = ex;
            DeviceName = deviceName;
        }
    }
}
