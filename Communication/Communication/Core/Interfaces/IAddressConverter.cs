namespace CommunicationNew.Core.Interfaces
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
