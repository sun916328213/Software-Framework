// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IAddressConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core;
namespace Communication.Core.Interfaces
{
    /// <summary>
    /// 地址转换器接口 —— 将用户友好地址转为协议特定地址
    /// 如 "Temperature" → ("HR100", 100, 2) 或 "D200" → (0xA8, 200)
    /// </summary>
    public interface IAddressConverter
    {
        /// <summary>协议类型</summary>
        ProtocolType ProtocolType { get; }

        /// <summary>将字符串地址解析为协议特定的地址结构</summary>
        (object region, int startAddress, int length) Convert(string address, int length);

        /// <summary>将协议特定的地址结构转为字符串地址</summary>
        string ToString(object region, int startAddress, int length);
    }
}
