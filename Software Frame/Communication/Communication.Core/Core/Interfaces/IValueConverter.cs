// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IValueConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core;
namespace Communication.Core.Interfaces
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
