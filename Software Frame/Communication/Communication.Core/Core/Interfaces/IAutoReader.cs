// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IAutoReader。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.EventArgs。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.EventArgs;

namespace Communication.Core.Interfaces
{
    /// <summary>
    /// 自动读取器接口 —— 后台定时轮询数据点
    /// </summary>
    public interface IAutoReader : IDisposable, IAsyncDisposable
    {
        /// <summary>是否正在运行</summary>
        bool IsRunning { get; }

        /// <summary>轮询间隔（毫秒）</summary>
        int IntervalMs { get; set; }

        /// <summary>数据点变化事件</summary>
        event EventHandler<DataPointChangedEventArgs>? DataPointChanged;

        /// <summary>启动自动读取；释放开始后拒绝新的轮询。</summary>
        /// <exception cref="ObjectDisposedException">读取器已经开始释放。</exception>
        void Start();

        /// <summary>停止自动读取</summary>
        void Stop();

        /// <summary>取消轮询并等待当前读取操作退出。</summary>
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
