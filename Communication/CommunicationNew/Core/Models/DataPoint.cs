using CommunicationNew.Core.Converters;
using CommunicationNew.Core.Interfaces;
using CommunicationNew.Abstractions;

namespace CommunicationNew.Core.Models
{
    /// <summary>
    /// 数据点实现 —— 将协议地址映射为有名称、有类型的变量
    /// </summary>
    public class DataPoint : IDataPoint
    {
        private byte[] _rawValue = Array.Empty<byte>();
        private object? _typedValue;
        private DataQuality _quality = DataQuality.Uncertain;
        private DateTime _timestamp = DateTime.MinValue;

        public string Name { get; internal set; } = "";

        public string Address { get; set; } = "";

        public DataPointType DataType { get; internal set; } = DataPointType.Int16;

        /// <summary>该点位使用的设备字节与字顺序。</summary>
        public DataFormat DataFormat { get; set; } = DataFormat.ABCD;

        public int Length { get; set; } = 0;

        public byte[] RawValue
        {
            get => _rawValue;
            set
            {
                _rawValue = value;
                Refresh();
                Timestamp = DateTime.Now;
            }
        }

        public object? Value => _typedValue;

        public DateTime Timestamp
        {
            get => _timestamp;
            set => _timestamp = value;
        }

        public DataQuality Quality
        {
            get => _quality;
            set => _quality = value;
        }

        public bool Readable { get; set; } = true;

        public bool Writable { get; set; } = true;

        public DataPoint() { }

        public DataPoint(string name, DataPointType type, string address, int length = 0)
        {
            Name = name;
            DataType = type;
            Address = address;
            Length = length > 0
                ? length
                : GetByteLength(type);
        }

        /// <summary>刷新值（从原始字节解码为类型化值）</summary>
        public void Refresh()
        {
            var converter = ValueConverterFactory.Create(DataType, DataFormat);
            _typedValue = converter.Decode(_rawValue);
        }

        /// <summary>从类型化值编码为原始字节</summary>
        public void Encode(object? value)
        {
            var converter = ValueConverterFactory.Create(DataType, DataFormat);
            _rawValue = converter.Encode(value);
            _typedValue = value;
        }

        /// <summary>获取该类型所需的字节数</summary>
        public static int GetByteLength(DataPointType type) => type switch
        {
            DataPointType.Bool => 1,
            DataPointType.Byte => 1,
            DataPointType.Int16 => 2,
            DataPointType.UInt16 => 2,
            DataPointType.Int32 => 4,
            DataPointType.UInt32 => 4,
            DataPointType.Int64 => 8,
            DataPointType.Float => 4,
            DataPointType.Double => 8,
            _ => 32,
        };

        public override string ToString() => $"{Name}: {Value} [{DataType}] @ {Timestamp:HH:mm:ss.fff}";
    }

    /// <summary>
    /// 泛型数据点实现
    /// </summary>
    public class DataPoint<T> : DataPoint, IDataPoint<T>
    {
        public DataPoint(string name, string address, int length = 0)
            : base(name, TypeToDataPointType(typeof(T)), address, length)
        {
        }

        T? IDataPoint<T>.Value
        {
            get => As<T>();
            set => (this as IDataPoint).Encode(value);
        }

        /// <summary>强类型读取</summary>
        public TValue? As<TValue>()
        {
            var v = Value;
            if (v is TValue t) return t;
            if (v != null)
            {
                try { return (TValue)Convert.ChangeType(v, typeof(TValue)); }
                catch { return default; }
            }
            return default;
        }

        private static DataPointType TypeToDataPointType(Type type)
        {
            if (type == typeof(bool)) return DataPointType.Bool;
            if (type == typeof(byte)) return DataPointType.Byte;
            if (type == typeof(short)) return DataPointType.Int16;
            if (type == typeof(ushort)) return DataPointType.UInt16;
            if (type == typeof(int)) return DataPointType.Int32;
            if (type == typeof(uint)) return DataPointType.UInt32;
            if (type == typeof(long)) return DataPointType.Int64;
            if (type == typeof(float)) return DataPointType.Float;
            if (type == typeof(double)) return DataPointType.Double;
            if (type == typeof(string)) return DataPointType.String;
            if (type == typeof(byte[])) return DataPointType.ByteArray;
            return DataPointType.Int16;
        }
    }
}
