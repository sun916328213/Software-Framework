// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：DataReceivedEventArgs。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core;
namespace Communication.Core.EventArgs
{
    /// <summary>
    /// 数据接收事件参数
    /// </summary>
    public class DataReceivedEventArgs : System.EventArgs
    {
        /// <summary>接收到的原始数据</summary>
        public byte[] Data { get; }

        /// <summary>数据长度</summary>
        public int Length => Data?.Length ?? 0;

        /// <summary>来源地址（如远程IP:Port，或COM端口名）</summary>
        public string? RemoteAddress { get; }

        /// <summary>接收时间</summary>
        public DateTime Timestamp { get; } = DateTime.Now;

        /// <summary>
        /// 初始化 DataReceivedEventArgs 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public DataReceivedEventArgs(byte[] data, string? remoteAddress = null)
        {
            Data = data;
            RemoteAddress = remoteAddress;
        }
    }
}
