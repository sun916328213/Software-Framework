using CommunicationNew.Core.Interfaces;
using CommunicationNew.Abstractions;

namespace CommunicationNew.Core.Models
{
    /// <summary>
    /// 数据点定义 —— 用于配置侧声明需要读写的数据点（DTO, 可序列化）
    /// </summary>
    public class DataPointDefinition
    {
        /// <summary>变量名称</summary>
        public string Name { get; set; } = "";

        /// <summary>协议地址</summary>
        public string Address { get; set; } = "";

        /// <summary>数据类型</summary>
        public DataPointType DataType { get; set; } = DataPointType.Int16;

        /// <summary>设备侧字节与字顺序。</summary>
        public DataFormat DataFormat { get; set; } = DataFormat.ABCD;

        /// <summary>读写方向（默认读写）</summary>
        public DataPointAccess Access { get; set; } = DataPointAccess.ReadWrite;

        /// <summary>描述</summary>
        public string Description { get; set; } = "";

        /// <summary>可读</summary>
        public bool Readable => Access is DataPointAccess.Read or DataPointAccess.ReadWrite;

        /// <summary>可写</summary>
        public bool Writable => Access is DataPointAccess.Write or DataPointAccess.ReadWrite;

        /// <summary>该定义所需字节数；未设置或为 0 时使用数据类型的默认长度，负数无效。</summary>
        public int? LengthBytes { get; set; }

        /// <summary>
        /// 点位扫描周期（毫秒）。0 表示继承轮询器的默认周期。
        /// </summary>
        public int ScanIntervalMs { get; set; }

        /// <summary>计算字节数</summary>
        public int GetByteLength()
        {
            if (LengthBytes < 0)
            {
                // 负数是配置错误，必须在定义转换时暴露，不能由数据点构造器静默修正。
                throw new ArgumentOutOfRangeException(
                    nameof(LengthBytes),
                    LengthBytes,
                    "数据点字节长度不能为负数。");
            }

            // 配置文件常用 0 表示“未覆盖”，在此统一解析，避免不同实例化入口产生不同长度。
            return LengthBytes is null or 0
                ? DataPoint.GetByteLength(DataType)
                : LengthBytes.Value;
        }

        /// <summary>将定义实例化为数据点</summary>
        public IDataPoint ToDataPoint() => new DataPoint(Name, DataType, Address, GetByteLength())
        {
            DataFormat = DataFormat,
            Readable = Readable,
            Writable = Writable,
            ScanIntervalMs = ScanIntervalMs,
        };
    }

    /// <summary>
    /// 数据点读写方向
    /// </summary>
    public enum DataPointAccess
    {
        /// <summary>只读</summary>
        Read,
        /// <summary>只写</summary>
        Write,
        /// <summary>读写</summary>
        ReadWrite,
    }
}
