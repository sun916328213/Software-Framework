// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：DataPoint。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Converters、Communication.Core.Interfaces、Communication.Core.Abstractions。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Converters;
using Communication.Core.Interfaces;
using Communication.Core.Abstractions;

namespace Communication.Core.Models
{
    /// <summary>
    /// 数据点实现 —— 将协议地址映射为有名称、有类型的变量
    /// </summary>
    public class DataPoint : IDataPoint
    {
        private byte[] _rawValue = Array.Empty<byte>();
        private string _address = "";
        private int _length;
        private int _scanIntervalMs;
        private readonly object _valueLock = new();
        private ValueUpdate<object?> _current = new(
            null,
            DataQuality.Uncertain,
            DateTimeOffset.MinValue);

        public string Name { get; internal set; } = "";

        public string Address
        {
            get => _address;
            set
            {
                // Core 只保证地址存在；具体语法由对应协议 adapter 解析和验证。
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
                _address = value;
            }
        }

        public DataPointType DataType { get; internal set; } = DataPointType.Int16;

        /// <summary>该点位使用的设备字节与字顺序。</summary>
        public DataFormat DataFormat { get; set; } = DataFormat.ABCD;

        public int Length
        {
            get => _length;
            set
            {
                if (value < 0)
                {
                    // 负长度无法对应任何有效报文范围，必须在进入规划和读写流程前拒绝。
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "数据点长度不能为负数。");
                }

                var minimumLength = DataType is DataPointType.String or DataPointType.ByteArray
                    ? 0
                    : GetByteLength(DataType);
                if (value > 0 && value < minimumLength)
                {
                    // 固定宽度值若读取不足，所有采样都会解码失败，应在配置进入运行时前拒绝。
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        $"{DataType} 数据点长度不能小于 {minimumLength} 字节。");
                }

                _length = value;
            }
        }

        public byte[] RawValue
        {
            get
            {
                lock (_valueLock)
                {
                    return _rawValue.ToArray();
                }
            }
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                var converter = ValueConverterFactory.Create(DataType, DataFormat);
                EnsureDecodableLength(converter, value);
                var decoded = converter.Decode(value);
                lock (_valueLock)
                {
                    _rawValue = value.ToArray();
                    _current = new ValueUpdate<object?>(
                        decoded,
                        _current.Quality,
                        DateTimeOffset.Now,
                        error: null);
                }
            }
        }

        public object? Value => Current.Value;

        /// <inheritdoc />
        public ValueUpdate<object?> Current
        {
            get
            {
                lock (_valueLock)
                {
                    return _current;
                }
            }
        }

        public DateTime Timestamp
        {
            get => Current.Timestamp.LocalDateTime;
            set
            {
                lock (_valueLock)
                {
                    _current = _current with { Timestamp = new DateTimeOffset(value) };
                }
            }
        }

        public DataQuality Quality
        {
            get => Current.Quality;
            set
            {
                lock (_valueLock)
                {
                    _current = _current with { Quality = value };
                }
            }
        }

        public bool Readable { get; set; } = true;

        public bool Writable { get; set; } = true;

        /// <inheritdoc />
        public int ScanIntervalMs
        {
            get => _scanIntervalMs;
            set
            {
                if (value < 0)
                {
                    // 无效周期应在配置进入运行时点位时失败，不能推迟到后台调度阶段。
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "数据点扫描周期不能为负数。");
                }

                _scanIntervalMs = value;
            }
        }

        /// <summary>
        /// 初始化 DataPoint 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public DataPoint() { }

        /// <summary>
        /// 初始化 DataPoint 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 ThrowIfNullOrWhiteSpace、GetByteLength；具体条件和先后次序见方法体。
        /// </summary>
        public DataPoint(string name, DataPointType type, string address, int length = 0)
        {
            // 运行时点位必须具有可索引的名称；默认无参构造仍供序列化器逐项赋值。
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            // 即使调用方显式指定长度，也必须先验证类型，不能创建无法解码的运行时点位。
            var defaultLength = GetByteLength(type);
            Name = name;
            DataType = type;
            Address = address;
            // 只有 0 表示使用类型默认长度；负数交给 Length 的统一不变量校验。
            Length = length == 0
                ? defaultLength
                : length;
        }

        /// <summary>刷新值（从原始字节解码为类型化值）</summary>
        public void Refresh()
        {
            var converter = ValueConverterFactory.Create(DataType, DataFormat);
            lock (_valueLock)
            {
                EnsureDecodableLength(converter, _rawValue);
                var decoded = converter.Decode(_rawValue);
                _current = _current with { Value = decoded, Error = null };
            }
        }

        /// <summary>从类型化值编码为原始字节</summary>
        public void Encode(object? value)
        {
            var converter = ValueConverterFactory.Create(DataType, DataFormat);
            var encoded = converter.Encode(value);
            lock (_valueLock)
            {
                _rawValue = encoded;
                _current = _current with { Value = value, Error = null };
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ApplyRead。
        /// 正文中的关键协作调用包括 ThrowIfNull、Create、EnsureDecodableLength、Decode、ToArray；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public void ApplyRead(
            byte[] rawValue,
            DateTimeOffset timestamp,
            DateTimeOffset? sourceTimestamp = null)
        {
            ArgumentNullException.ThrowIfNull(rawValue);
            var converter = ValueConverterFactory.Create(DataType, DataFormat);
            // 先在快照之外解码；解码失败时不能破坏最后一个有效样本。
            EnsureDecodableLength(converter, rawValue);
            var decoded = converter.Decode(rawValue);
            lock (_valueLock)
            {
                _rawValue = rawValue.ToArray();
                _current = new ValueUpdate<object?>(
                    decoded,
                    DataQuality.Good,
                    timestamp,
                    sourceTimestamp);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ApplyFailure。
        /// 正文中的关键协作调用包括 ThrowIfNull；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public void ApplyFailure(Exception error, DateTimeOffset timestamp)
        {
            ArgumentNullException.ThrowIfNull(error);
            lock (_valueLock)
            {
                _current = new ValueUpdate<object?>(
                    _current.Value,
                    DataQuality.Bad,
                    timestamp,
                    error: error);
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EnsureDecodableLength。
        /// 正文中的关键协作调用包括 GetByteCount；具体条件和先后次序见方法体。
        /// </summary>
        private static void EnsureDecodableLength(IValueConverter converter, byte[] rawValue)
        {
            var requiredLength = converter.GetByteCount();
            if (requiredLength > 0 && rawValue.Length < requiredLength)
            {
                throw new InvalidDataException(
                    $"数据长度 {rawValue.Length} 不足，{converter.TargetType} 至少需要 {requiredLength} 字节。");
            }
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
            _ => throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "不支持的数据点类型。"),
        };

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </summary>
        public override string ToString() => $"{Name}: {Value} [{DataType}] @ {Timestamp:HH:mm:ss.fff}";
    }

    /// <summary>
    /// 泛型数据点实现
    /// </summary>
    public class DataPoint<T> : DataPoint, IDataPoint<T>
    {
        /// <summary>
        /// 初始化 DataPoint 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 TypeToDataPointType；具体条件和先后次序见方法体。
        /// </summary>
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
        /// <exception cref="InvalidCastException">当前值无法转换为目标类型。</exception>
        public TValue? As<TValue>()
        {
            var v = Value;
            if (v is TValue t) return t;
            if (v != null)
            {
                try
                {
                    return (TValue)Convert.ChangeType(v, typeof(TValue));
                }
                catch (Exception ex) when (
                    ex is InvalidCastException or FormatException or OverflowException)
                {
                    // 转换失败必须显式暴露，不能把 default 伪装成设备返回的合法值。
                    throw new InvalidCastException(
                        $"无法将数据点值从 {v.GetType().FullName} 转换为 " +
                        $"{typeof(TValue).FullName ?? typeof(TValue).Name}。",
                        ex);
                }
            }
            return default;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 TypeToDataPointType。
        /// </summary>
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
            // 未知泛型不能伪装成 Int16，否则字节长度和解码结果都会与调用方类型不一致。
            throw new NotSupportedException(
                $"数据点不支持强类型 {type.FullName ?? type.Name}。");
        }
    }
}
