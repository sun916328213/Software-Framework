namespace CommunicationNew.Core.EventArgs
{
    /// <summary>
    /// 数据点变化事件参数
    /// </summary>
    public class DataPointChangedEventArgs : System.EventArgs
    {
        /// <summary>变化的数据点</summary>
        public Interfaces.IDataPoint DataPoint { get; }

        /// <summary>旧值</summary>
        public object? OldValue { get; }

        /// <summary>新值</summary>
        public object? NewValue { get; }

        /// <summary>变化时间</summary>
        public DateTime Timestamp { get; } = DateTime.Now;

        public DataPointChangedEventArgs(Interfaces.IDataPoint dataPoint, object? oldValue, object? newValue)
        {
            DataPoint = dataPoint;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
