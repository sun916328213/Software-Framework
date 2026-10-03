// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ConnectionStateChangedEventArgs。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core;
namespace Communication.Core.EventArgs
{
    /// <summary>
    /// 连接状态变化事件参数
    /// </summary>
    public class ConnectionStateChangedEventArgs : System.EventArgs
    {
        /// <summary>是否已连接</summary>
        public bool IsConnected { get; }

        /// <summary>设备名称</summary>
        public string DeviceName { get; }

        /// <summary>变化时间</summary>
        public DateTime Timestamp { get; } = DateTime.Now;

        /// <summary>附加消息</summary>
        public string? Message { get; }

        /// <summary>
        /// 初始化 ConnectionStateChangedEventArgs 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ConnectionStateChangedEventArgs(bool isConnected, string deviceName, string? message = null)
        {
            IsConnected = isConnected;
            DeviceName = deviceName;
            Message = message;
        }
    }
}
