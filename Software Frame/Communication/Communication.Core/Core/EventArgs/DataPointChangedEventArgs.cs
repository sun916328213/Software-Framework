// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：DataPointChangedEventArgs。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core;
namespace Communication.Core.EventArgs
{
    /// <summary>
    /// 数据点变化事件参数
    /// </summary>
    public class DataPointChangedEventArgs : System.EventArgs
    {
        /// <summary>变化的数据点</summary>
        public Interfaces.IDataPoint DataPoint { get; }

        /// <summary>旧值</summary>
        public object? OldValue { get; }

        /// <summary>新值</summary>
        public object? NewValue { get; }

        /// <summary>变化时间</summary>
        public DateTime Timestamp { get; } = DateTime.Now;

        /// <summary>
        /// 初始化 DataPointChangedEventArgs 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public DataPointChangedEventArgs(Interfaces.IDataPoint dataPoint, object? oldValue, object? newValue)
        {
            DataPoint = dataPoint;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
