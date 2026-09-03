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

        /// <summary>该定义所需字节数（0 表示使用类型默认值）</summary>
        public int? LengthBytes { get; set; }

        /// <summary>计算字节数</summary>
        public int GetByteLength() => LengthBytes ?? DataPoint.GetByteLength(DataType);

        /// <summary>将定义实例化为数据点</summary>
        public IDataPoint ToDataPoint() => new DataPoint(Name, DataType, Address, GetByteLength())
        {
            DataFormat = DataFormat,
            Readable = Readable,
            Writable = Writable,
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
