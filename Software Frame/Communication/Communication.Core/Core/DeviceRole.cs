// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：DeviceRole。成员说明可结合下方具体实现阅读。
namespace Communication.Core
{
    /// <summary>
    /// 设备角色：客户端 / 服务器
    /// </summary>
    public enum DeviceRole
    {
        /// <summary>客户端（主动连接）</summary>
        Client,
        /// <summary>服务器（被动监听）</summary>
        Server,
    }
}
