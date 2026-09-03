namespace CommunicationNew.Core.EventArgs
{
    /// <summary>
    /// 数据接收事件参数
    /// </summary>
    public class DataReceivedEventArgs : System.EventArgs
    {
        /// <summary>接收到的原始数据</summary>
        public byte[] Data { get; }

        /// <summary>数据长度</summary>
        public int Length => Data?.Length ?? 0;

        /// <summary>来源地址（如远程IP:Port，或COM端口名）</summary>
        public string? RemoteAddress { get; }

        /// <summary>接收时间</summary>
        public DateTime Timestamp { get; } = DateTime.Now;

        public DataReceivedEventArgs(byte[] data, string? remoteAddress = null)
        {
            Data = data;
            RemoteAddress = remoteAddress;
        }
    }
}
