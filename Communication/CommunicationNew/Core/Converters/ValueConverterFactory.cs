using CommunicationNew.Core.Interfaces;
using CommunicationNew.Abstractions;
using CommunicationNew.Runtime.Conversion;
using System.Collections.Concurrent;

namespace CommunicationNew.Core.Converters
{
    /// <summary>
    /// 值转换器工厂 —— 按数据类型返回对应转换器实例
    /// </summary>
    public static class ValueConverterFactory
    {
        private static readonly ConcurrentDictionary<(DataPointType Type, DataFormat Format), IValueConverter> Cache = new();

        /// <summary>获取类型对应的转换器</summary>
        public static IValueConverter Create(
            DataPointType type,
            DataFormat format = DataFormat.ABCD) =>
            Cache.GetOrAdd((type, format), static key => CreateConverter(key.Type, key.Format));

        /// <summary>注册自定义转换器（可覆盖默认）</summary>
        public static void Register(
            IValueConverter converter,
            DataFormat format = DataFormat.ABCD)
        {
            ArgumentNullException.ThrowIfNull(converter);
            Cache[(converter.TargetType, format)] = converter;
        }

        private static IValueConverter CreateConverter(DataPointType type, DataFormat format)
        {
            var codec = new DataFormatCodec(format);
            return type switch
            {
                DataPointType.Bool => new BoolConverter(),
                DataPointType.Byte => new ByteConverter(),
                DataPointType.Int16 => new Int16Converter(codec),
                DataPointType.UInt16 => new UInt16Converter(codec),
                DataPointType.Int32 => new Int32Converter(codec),
                DataPointType.UInt32 => new UInt32Converter(codec),
                DataPointType.Int64 => new Int64Converter(format),
                DataPointType.Float => new FloatConverter(codec),
                DataPointType.Double => new DoubleConverter(format),
                DataPointType.String => new StringConverter(),
                DataPointType.ByteArray => new ByteArrayConverter(),
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
        }
    }
}
