// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：CommunicationParam。成员说明可结合下方具体实现阅读。
namespace Communication.Core
{
    /// <summary>
    /// 通信参数基类 —— 每种协议派生出自己的参数子类。
    /// 通过统一 Param 体系，外部代码只需更换 Param 类型即可切换协议。
    /// </summary>
    public abstract class CommunicationParam
    {
        /// <summary>协议类型</summary>
        public ProtocolType ProtocolType { get; set; }

        /// <summary>角色</summary>
        public DeviceRole Role { get; set; } = DeviceRole.Client;

        /// <summary>设备名称</summary>
        public string DeviceName { get; set; } = "";

        /// <summary>设备唯一ID（自动生成）</summary>
        public string DeviceId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>单次读写超时（毫秒）</summary>
        public int TimeoutMs { get; set; } = 3000;

        /// <summary>重试次数</summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>重试间隔（毫秒）</summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>是否自动重连</summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>自动重连间隔（毫秒）</summary>
        public int ReconnectIntervalMs { get; set; } = 3000;

        /// <summary>异步用户事件队列容量；队列满时丢弃最旧通知。</summary>
        public int EventQueueCapacity { get; set; } = 256;

        /// <summary>
        /// 被动接收收件箱容量。TCP/Serial 满载时停止连接，UDP 满载时丢弃最旧数据报。
        /// </summary>
        public int ReceiveQueueCapacity { get; set; } = 256;

        /// <summary>
        /// 等待单请求执行权的队列容量；队列满时明确拒绝新请求。
        /// </summary>
        public int RequestQueueCapacity { get; set; } = 256;

        /// <summary>
        /// 服务端允许同时处理的在途请求数；容量耗尽时新请求会被明确拒绝。
        /// </summary>
        public int ServerRequestCapacity { get; set; } = 100;
    }
}
