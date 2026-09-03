using CommunicationNew.Core.EventArgs;

namespace CommunicationNew.Core.Interfaces
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

        /// <summary>启动自动读取</summary>
        void Start();

        /// <summary>停止自动读取</summary>
        void Stop();

        /// <summary>取消轮询并等待当前读取操作退出。</summary>
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
