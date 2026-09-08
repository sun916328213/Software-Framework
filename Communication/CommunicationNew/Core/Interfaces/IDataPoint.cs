namespace CommunicationNew.Core.Interfaces
{
    using CommunicationNew.Core.Models;

    /// <summary>
    /// 数据质量标志
    /// </summary>
    public enum DataQuality
    {
        /// <summary>好</summary>
        Good,
        /// <summary>坏</summary>
        Bad,
        /// <summary>未知/未刷新</summary>
        Uncertain,
    }

    /// <summary>
    /// 数据点数据类型
    /// </summary>
    public enum DataPointType
    {
        Bool,
        Byte,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        Float,
        Double,
        String,
        ByteArray,
    }

    /// <summary>
    /// 数据点接口 —— 将协议地址映射为有名称、有类型的变量
    /// </summary>
    public interface IDataPoint
    {
        /// <summary>变量名称（如 "Temperature", "Speed"）</summary>
        string Name { get; }

        /// <summary>协议地址（如 "HR100", "D200", "ns=2;s=Temp"）</summary>
        string Address { get; set; }

        /// <summary>数据类型</summary>
        DataPointType DataType { get; }

        /// <summary>读取长度（字节数或寄存器数）</summary>
        int Length { get; set; }

        /// <summary>当前值（原始字节）</summary>
        byte[] RawValue { get; set; }

        /// <summary>当前值（类型化）</summary>
        object? Value { get; }

        /// <summary>当前值、质量、时间与错误的不可变快照。</summary>
        ValueUpdate<object?> Current { get; }

        /// <summary>最后更新时间</summary>
        DateTime Timestamp { get; set; }

        /// <summary>数据质量</summary>
        DataQuality Quality { get; set; }

        /// <summary>是否可读</summary>
        bool Readable { get; set; }

        /// <summary>是否可写</summary>
        bool Writable { get; set; }

        /// <summary>
        /// 点位扫描周期（毫秒）。0 表示继承轮询器的默认周期。
        /// </summary>
        int ScanIntervalMs { get; set; }

        /// <summary>刷新值（从原始字节解码为类型化值）</summary>
        void Refresh();

        /// <summary>从类型化值编码为原始字节</summary>
        void Encode(object? value);

        /// <summary>解码并原子发布一次成功采样。</summary>
        void ApplyRead(
            byte[] rawValue,
            DateTimeOffset timestamp,
            DateTimeOffset? sourceTimestamp = null);

        /// <summary>保留最后一个有效值，并原子发布一次失败采样。</summary>
        void ApplyFailure(Exception error, DateTimeOffset timestamp);
    }

    /// <summary>
    /// 泛型数据点接口
    /// </summary>
    public interface IDataPoint<T> : IDataPoint
    {
        /// <summary>强类型当前值</summary>
        new T? Value { get; set; }
    }
}
