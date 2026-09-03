namespace CommunicationNew.Core.Interfaces
{
    /// <summary>
    /// 值转换器接口 —— 在 byte[] 与类型化值之间转换
    /// </summary>
    public interface IValueConverter
    {
        /// <summary>支持的数据类型</summary>
        DataPointType TargetType { get; }

        /// <summary>从字节解码为类型化值</summary>
        object? Decode(byte[] data, int offset = 0);

        /// <summary>从类型化值编码为字节</summary>
        byte[] Encode(object? value);

        /// <summary>获取该类型所需的字节数</summary>
        int GetByteCount();
    }

    /// <summary>
    /// 泛型值转换器
    /// </summary>
    public interface IValueConverter<T> : IValueConverter
    {
        /// <summary>从字节解码为强类型值</summary>
        new T? Decode(byte[] data, int offset = 0);

        /// <summary>从强类型值编码为字节</summary>
        byte[] Encode(T? value);
    }
}
