namespace CommunicationNew.Core
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
