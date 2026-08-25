using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Extension.DesignPattern.MediatorPattern
{
    /// <summary>
    /// 中介者接口
    /// 定义中介者的基本操作，用于协调同事对象之间的交互
    /// </summary>
    public interface IMediator : IDisposable
    {
        /// <summary>
        /// 注册同事对象
        /// </summary>
        /// <param name="colleague">同事对象</param>
        void Register(IColleague colleague);

        /// <summary>
        /// 取消注册同事对象
        /// </summary>
        /// <param name="colleague">同事对象</param>
        void Unregister(IColleague colleague);

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="from">发送者</param>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息内容</param>
        void Send(string from, string to, object message);

        /// <summary>
        /// 异步发送消息
        /// </summary>
        /// <param name="from">发送者</param>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息内容</param>
        /// <returns>表示异步操作的任务</returns>
        Task SendAsync(string from, string to, object message);

        /// <summary>
        /// 广播消息
        /// </summary>
        /// <param name="from">发送者</param>
        /// <param name="message">消息内容</param>
        void Broadcast(string from, object message);

        /// <summary>
        /// 异步广播消息
        /// </summary>
        /// <param name="from">发送者</param>
        /// <param name="message">消息内容</param>
        /// <returns>表示异步操作的任务</returns>
        Task BroadcastAsync(string from, object message);
    }

    /// <summary>
    /// 同事接口
    /// 定义同事对象的基本行为
    /// </summary>
    public interface IColleague
    {
        /// <summary>
        /// 获取同事标识
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 设置中介者
        /// </summary>
        /// <param name="mediator">中介者</param>
        void SetMediator(IMediator mediator);

        /// <summary>
        /// 接收消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息内容</param>
        void Receive(string from, object message);

        /// <summary>
        /// 异步接收消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息内容</param>
        /// <returns>表示异步操作的任务</returns>
        Task ReceiveAsync(string from, object message);

        /// <summary>
        /// 发送消息给指定同事
        /// </summary>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息内容</param>
        void Send(string to, object message);

        /// <summary>
        /// 异步发送消息给指定同事
        /// </summary>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息内容</param>
        /// <returns>表示异步操作的任务</returns>
        Task SendAsync(string to, object message);

        /// <summary>
        /// 广播消息给所有同事
        /// </summary>
        /// <param name="message">消息内容</param>
        void Broadcast(object message);

        /// <summary>
        /// 异步广播消息给所有同事
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <returns>表示异步操作的任务</returns>
        Task BroadcastAsync(object message);
    }

    /// <summary>
    /// 抽象同事基类
    /// 提供同事对象的基础实现
    /// </summary>
    public abstract class ColleagueBase : IColleague
    {
        private IMediator _mediator;
        private bool _disposed = false;

        /// <summary>
        /// 获取同事标识
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="id">同事标识</param>
        protected ColleagueBase(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        /// <summary>
        /// 设置中介者
        /// </summary>
        /// <param name="mediator">中介者</param>
        public void SetMediator(IMediator mediator)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ColleagueBase));

            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        }

        /// <summary>
        /// 接收消息
        /// 子类应重写此方法实现具体的消息处理逻辑
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息内容</param>
        public abstract void Receive(string from, object message);

        /// <summary>
        /// 异步接收消息
        /// 子类应重写此方法实现具体的异步消息处理逻辑
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息内容</param>
        /// <returns>表示异步操作的任务</returns>
        public virtual async Task ReceiveAsync(string from, object message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ColleagueBase));

            // 默认实现调用同步方法
            Receive(from, message);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 发送消息给指定同事
        /// </summary>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息内容</param>
        public void Send(string to, object message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ColleagueBase));

            _mediator?.Send(Id, to, message);
        }

        /// <summary>
        /// 异步发送消息给指定同事
        /// </summary>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息内容</param>
        /// <returns>表示异步操作的任务</returns>
        public async Task SendAsync(string to, object message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ColleagueBase));

            if (_mediator != null)
            {
                await _mediator.SendAsync(Id, to, message);
            }
        }

        /// <summary>
        /// 广播消息给所有同事
        /// </summary>
        /// <param name="message">消息内容</param>
        public void Broadcast(object message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ColleagueBase));

            _mediator?.Broadcast(Id, message);
        }

        /// <summary>
        /// 异步广播消息给所有同事
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <returns>表示异步操作的任务</returns>
        public async Task BroadcastAsync(object message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ColleagueBase));

            if (_mediator != null)
            {
                await _mediator.BroadcastAsync(Id, message);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在 disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 具体中介者实现
    /// 协调同事对象之间的交互
    /// </summary>
    public class Mediator : IMediator
    {
        private readonly ConcurrentDictionary<string, IColleague> _colleagues = new ConcurrentDictionary<string, IColleague>();
        private readonly object _lock = new object();
        private bool _disposed = false;

        /// <summary>
        /// 注册同事对象
        /// </summary>
        /// <param name="colleague">同事对象</param>
        public void Register(IColleague colleague)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Mediator));

            if (colleague == null)
                throw new ArgumentNullException(nameof(colleague));

            if (string.IsNullOrEmpty(colleague.Id))
                throw new ArgumentException("同事标识不能为空", nameof(colleague.Id));

            // 设置同事的中介者
            colleague.SetMediator(this);

            // 注册同事
            _colleagues.TryAdd(colleague.Id, colleague);
        }

        /// <summary>
        /// 取消注册同事对象
        /// </summary>
        /// <param name="colleague">同事对象</param>
        public void Unregister(IColleague colleague)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Mediator));

            if (colleague == null)
                throw new ArgumentNullException(nameof(colleague));

            _colleagues.TryRemove(colleague.Id, out _);
        }

        /// <summary>
        /// 批量注册同事对象
        /// </summary>
        /// <param name="colleagues">同事对象集合</param>
        public void RegisterRange(IEnumerable<IColleague> colleagues)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Mediator));

            if (colleagues == null)
                throw new ArgumentNullException(nameof(colleagues));

            foreach (var colleague in colleagues)
            {
                if (colleague != null)
                {
                    Register(colleague);
                }
            }
        }

        /// <summary>
        /// 批量取消注册同事对象
        /// </summary>
        /// <param name="colleagues">同事对象集合</param>
        public void UnregisterRange(IEnumerable<IColleague> colleagues)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Mediator));

            if (colleagues == null)
                throw new ArgumentNullException(nameof(colleagues));

            foreach (var colleague in colleagues)
            {
                if (colleague != null)
                {
                    Unregister(colleague);
                }
            }
        }

        /// <summary>
        /// 清空所有同事对象
        /// </summary>
        public void ClearColleagues()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Mediator));

            _colleagues.Clear();
        }

        /// <summary>
        /// 获取同事数量
        /// </summary>
        public int ColleagueCount => _colleagues.Count;

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息内容</param>
        public void Send(string from, string to, object message)
        {
            if (_disposed)
                return;

            if (string.IsNullOrEmpty(to))
                throw new ArgumentException("接收者标识不能为空", nameof(to));

            if (_colleagues.TryGetValue(to, out var colleague))
            {
                try
                {
                    colleague.Receive(from, message);
                }
                catch (Exception ex)
                {
                    // 记录异常但不中断其他操作
                    Console.WriteLine($"发送消息时发生异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 异步发送消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息内容</param>
        /// <returns>表示异步操作的任务</returns>
        public async Task SendAsync(string from, string to, object message)
        {
            if (_disposed)
                return;

            if (string.IsNullOrEmpty(to))
                throw new ArgumentException("接收者标识不能为空", nameof(to));

            if (_colleagues.TryGetValue(to, out var colleague))
            {
                try
                {
                    await colleague.ReceiveAsync(from, message);
                }
                catch (Exception ex)
                {
                    // 记录异常但不中断其他操作
                    Console.WriteLine($"发送消息时发生异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 广播消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息内容</param>
        public void Broadcast(string from, object message)
        {
            if (_disposed)
                return;

            // 创建同事列表的快照以避免在迭代时修改集合
            IColleague[] colleaguesCopy;
            lock (_lock)
            {
                colleaguesCopy = _colleagues.Values.ToArray();
            }

            foreach (var colleague in colleaguesCopy)
            {
                if (colleague == null || colleague.Id == from)
                    continue;

                try
                {
                    colleague.Receive(from, message);
                }
                catch (Exception ex)
                {
                    // 记录异常但不中断其他同事的处理
                    Console.WriteLine($"广播消息时发生异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 异步广播消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息内容</param>
        /// <returns>表示异步操作的任务</returns>
        public async Task BroadcastAsync(string from, object message)
        {
            if (_disposed)
                return;

            // 创建同事列表的快照以避免在迭代时修改集合
            IColleague[] colleaguesCopy;
            lock (_lock)
            {
                colleaguesCopy = _colleagues.Values.ToArray();
            }

            var tasks = new List<Task>();

            foreach (var colleague in colleaguesCopy)
            {
                if (colleague == null || colleague.Id == from)
                    continue;

                try
                {
                    tasks.Add(colleague.ReceiveAsync(from, message));
                }
                catch (Exception ex)
                {
                    // 记录异常但不中断其他同事的处理
                    Console.WriteLine($"广播消息时发生异常: {ex.Message}");
                }
            }

            // 等待所有任务完成
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在 disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 清空同事列表
                    ClearColleagues();
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 基于委托的同事实现
    /// 提供更灵活的消息处理方式
    /// </summary>
    public class DelegateColleague : ColleagueBase
    {
        private readonly Action<string, object> _receiveHandler;
        private readonly Func<string, object, Task> _receiveAsyncHandler;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="id">同事标识</param>
        /// <param name="receiveHandler">消息处理委托</param>
        /// <param name="receiveAsyncHandler">异步消息处理委托</param>
        public DelegateColleague(
            string id, 
            Action<string, object> receiveHandler,
            Func<string, object, Task> receiveAsyncHandler = null) : base(id)
        {
            _receiveHandler = receiveHandler ?? throw new ArgumentNullException(nameof(receiveHandler));
            _receiveAsyncHandler = receiveAsyncHandler;
        }

        /// <summary>
        /// 接收消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息内容</param>
        public override void Receive(string from, object message)
        {
            _receiveHandler?.Invoke(from, message);
        }

        /// <summary>
        /// 异步接收消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息内容</param>
        /// <returns>表示异步操作的任务</returns>
        public override async Task ReceiveAsync(string from, object message)
        {
            if (_receiveAsyncHandler != null)
            {
                await _receiveAsyncHandler(from, message);
            }
            else
            {
                // 如果没有提供异步处理委托，则使用同步方法
                Receive(from, message);
                await Task.CompletedTask;
            }
        }
    }
}