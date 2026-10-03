// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：CommunicationErrorEventArgs。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core;
namespace Communication.Core.EventArgs
{
    /// <summary>
    /// 通信错误事件参数
    /// </summary>
    public class CommunicationErrorEventArgs : System.EventArgs
    {
        /// <summary>错误异常</summary>
        public Exception Exception { get; }

        /// <summary>设备名称</summary>
        public string DeviceName { get; }

        /// <summary>错误时间</summary>
        public DateTime Timestamp { get; } = DateTime.Now;

        /// <summary>错误消息</summary>
        public string Message => Exception?.Message ?? "未知错误";

        /// <summary>
        /// 初始化 CommunicationErrorEventArgs 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public CommunicationErrorEventArgs(Exception ex, string deviceName)
        {
            Exception = ex;
            DeviceName = deviceName;
        }
    }
}
