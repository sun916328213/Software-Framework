// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ValueConverterFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Interfaces、Communication.Core.Abstractions、System.Collections.Concurrent。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Interfaces;
using Communication.Core.Abstractions;
using System.Collections.Concurrent;

namespace Communication.Core.Converters
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

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateConverter。
        /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
        /// </summary>
        private static IValueConverter CreateConverter(DataPointType type, DataFormat format)
        {
            return type switch
            {
                DataPointType.Bool => new BoolConverter(),
                DataPointType.Byte => new ByteConverter(),
                DataPointType.Int16 => new Int16Converter(format),
                DataPointType.UInt16 => new UInt16Converter(format),
                DataPointType.Int32 => new Int32Converter(format),
                DataPointType.UInt32 => new UInt32Converter(format),
                DataPointType.Int64 => new Int64Converter(format),
                DataPointType.Float => new FloatConverter(format),
                DataPointType.Double => new DoubleConverter(format),
                DataPointType.String => new StringConverter(),
                DataPointType.ByteArray => new ByteArrayConverter(),
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
        }
    }
}
