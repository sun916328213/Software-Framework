namespace CommunicationNew.Core.Models
{
    /// <summary>
    /// 设备值 —— 封装类型化值及其元数据（时间戳、质量）
    /// </summary>
    public class DeviceValue
    {
        /// <summary>值</summary>
        public object? Value { get; set; }

        /// <summary>原始字节</summary>
        public byte[]? RawData { get; set; }

        /// <summary>数据类型</summary>
        public Interfaces.DataPointType DataType { get; set; }

        /// <summary>最后更新时间</summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>数据质量</summary>
        public Interfaces.DataQuality Quality { get; set; } = Interfaces.DataQuality.Good;

        /// <summary>数据点名称</summary>
        public string? Name { get; set; }

        public DeviceValue() { }

        public DeviceValue(object? value, Interfaces.DataPointType type)
        {
            Value = value;
            DataType = type;
        }

        /// <summary>获取强类型值</summary>
        public T? GetValue<T>()
        {
            if (Value is T t) return t;
            if (Value != null)
            {
                try { return (T)Convert.ChangeType(Value, typeof(T)); }
                catch { return default; }
            }
            return default;
        }

        public override string ToString() => $"{Name ?? "?"}: {Value} [{DataType}] @ {Timestamp:HH:mm:ss.fff}";
    }
}
