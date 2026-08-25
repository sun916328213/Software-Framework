using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Extension.DesignPattern.MediatorPattern.Advanced
{
    /// <summary>
    /// 消息优先级枚举
    /// </summary>
    public enum MessagePriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// 带优先级的消息数据
    /// </summary>
    public class PriorityMessage
    {
        public object Data { get; }
        public MessagePriority Priority { get; }
        public DateTime CreationTime { get; }
        public string MessageType { get; }

        public PriorityMessage(object data, MessagePriority priority, string messageType = null)
        {
            Data = data;
            Priority = priority;
            CreationTime = DateTime.Now;
            MessageType = messageType;
        }
    }

    /// <summary>
    /// 消息过滤器接口
    /// </summary>
    public interface IMessageFilter
    {
        /// <summary>
        /// 判断消息是否应该被处理
        /// </summary>
        /// <param name="message">消息数据</param>
        /// <returns>如果应该处理返回true，否则返回false</returns>
        bool ShouldProcess(PriorityMessage message);
    }

    /// <summary>
    /// 类型过滤器
    /// </summary>
    public class TypeFilter : IMessageFilter
    {
        private readonly HashSet<string> _allowedTypes;

        public TypeFilter(IEnumerable<string> allowedTypes)
        {
            _allowedTypes = new HashSet<string>(allowedTypes ?? throw new ArgumentNullException(nameof(allowedTypes)));
        }

        public bool ShouldProcess(PriorityMessage message)
        {
            if (string.IsNullOrEmpty(message.MessageType))
                return _allowedTypes.Contains(string.Empty) || _allowedTypes.Count == 0;

            return _allowedTypes.Contains(message.MessageType);
        }
    }

    /// <summary>
    /// 优先级过滤器
    /// </summary>
    public class PriorityFilter : IMessageFilter
    {
        private readonly MessagePriority _minPriority;

        public PriorityFilter(MessagePriority minPriority)
        {
            _minPriority = minPriority;
        }

        public bool ShouldProcess(PriorityMessage message)
        {
            return message.Priority >= _minPriority;
        }
    }

    /// <summary>
    /// 组合过滤器
    /// </summary>
    public class CompositeFilter : IMessageFilter
    {
        private readonly List<IMessageFilter> _filters;

        public CompositeFilter(IEnumerable<IMessageFilter> filters)
        {
            _filters = new List<IMessageFilter>(filters ?? throw new ArgumentNullException(nameof(filters)));
        }

        public bool ShouldProcess(PriorityMessage message)
        {
            return _filters.All(filter => filter.ShouldProcess(message));
        }
    }

    /// <summary>
    /// 带过滤器的同事实现
    /// </summary>
    public class FilteredColleague : IColleague
    {
        private readonly IColleague _innerColleague;
        private readonly IMessageFilter _filter;
        private IMediator _mediator;
        private bool _disposed = false;

        public string Id => _innerColleague.Id;

        public FilteredColleague(IColleague innerColleague, IMessageFilter filter)
        {
            _innerColleague = innerColleague ?? throw new ArgumentNullException(nameof(innerColleague));
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        }

        public void SetMediator(IMediator mediator)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FilteredColleague));

            _mediator = mediator;
            _innerColleague.SetMediator(mediator);
        }

        public void Receive(string from, object message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FilteredColleague));

            if (message is PriorityMessage priorityMessage && _filter.ShouldProcess(priorityMessage))
            {
                _innerColleague.Receive(from, message);
            }
        }

        public async Task ReceiveAsync(string from, object message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FilteredColleague));

            if (message is PriorityMessage priorityMessage && _filter.ShouldProcess(priorityMessage))
            {
                await _innerColleague.ReceiveAsync(from, message);
            }
            else
            {
                await Task.CompletedTask;
            }
        }

        public void Send(string to, object message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FilteredColleague));

            _mediator?.Send(Id, to, message);
        }

        public async Task SendAsync(string to, object message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FilteredColleague));

            if (_mediator != null)
            {
                await _mediator.SendAsync(Id, to, message);
            }
        }

        public void Broadcast(object message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FilteredColleague));

            _mediator?.Broadcast(Id, message);
        }

        public async Task BroadcastAsync(object message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FilteredColleague));

            if (_mediator != null)
            {
                await _mediator.BroadcastAsync(Id, message);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_innerColleague is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 高级中介者接口
    /// </summary>
    public interface IAdvancedMediator : IMediator
    {
        /// <summary>
        /// 注册带过滤器的同事对象
        /// </summary>
        /// <param name="colleague">同事对象</param>
        /// <param name="filter">消息过滤器</param>
        void RegisterWithFilter(IColleague colleague, IMessageFilter filter);

        /// <summary>
        /// 按优先级发送消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息内容</param>
        /// <param name="priority">消息优先级</param>
        void SendWithPriority(string from, string to, object message, MessagePriority priority);

        /// <summary>
        /// 异步按优先级发送消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息内容</param>
        /// <param name="priority">消息优先级</param>
        /// <returns>表示异步操作的任务</returns>
        Task SendWithPriorityAsync(string from, string to, object message, MessagePriority priority);

        /// <summary>
        /// 按优先级广播消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息内容</param>
        /// <param name="priority">消息优先级</param>
        void BroadcastWithPriority(string from, object message, MessagePriority priority);

        /// <summary>
        /// 异步按优先级广播消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息内容</param>
        /// <param name="priority">消息优先级</param>
        /// <returns>表示异步操作的任务</returns>
        Task BroadcastWithPriorityAsync(string from, object message, MessagePriority priority);
    }

    /// <summary>
    /// 高级中介者实现
    /// 支持消息过滤、优先级处理等功能
    /// </summary>
    public class AdvancedMediator : IAdvancedMediator
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
                throw new ObjectDisposedException(nameof(AdvancedMediator));

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
        /// 注册带过滤器的同事对象
        /// </summary>
        /// <param name="colleague">同事对象</param>
        /// <param name="filter">消息过滤器</param>
        public void RegisterWithFilter(IColleague colleague, IMessageFilter filter)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedMediator));

            if (colleague == null)
                throw new ArgumentNullException(nameof(colleague));

            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            var filteredColleague = new FilteredColleague(colleague, filter);
            Register(filteredColleague);
        }

        /// <summary>
        /// 取消注册同事对象
        /// </summary>
        /// <param name="colleague">同事对象</param>
        public void Unregister(IColleague colleague)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedMediator));

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
                throw new ObjectDisposedException(nameof(AdvancedMediator));

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
                throw new ObjectDisposedException(nameof(AdvancedMediator));

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
                throw new ObjectDisposedException(nameof(AdvancedMediator));

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
        /// 按优先级发送消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息内容</param>
        /// <param name="priority">消息优先级</param>
        public void SendWithPriority(string from, string to, object message, MessagePriority priority)
        {
            if (_disposed)
                return;

            var priorityMessage = new PriorityMessage(message, priority);
            Send(from, to, priorityMessage);
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
        /// 异步按优先级发送消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息内容</param>
        /// <param name="priority">消息优先级</param>
        /// <returns>表示异步操作的任务</returns>
        public async Task SendWithPriorityAsync(string from, string to, object message, MessagePriority priority)
        {
            if (_disposed)
                return;

            var priorityMessage = new PriorityMessage(message, priority);
            await SendAsync(from, to, priorityMessage);
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

            // 按优先级排序处理同事
            var sortedColleagues = colleaguesCopy.OrderBy(c => GetColleaguePriority(c, message)).ToArray();

            foreach (var colleague in sortedColleagues)
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
        /// 按优先级广播消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息内容</param>
        /// <param name="priority">消息优先级</param>
        public void BroadcastWithPriority(string from, object message, MessagePriority priority)
        {
            if (_disposed)
                return;

            var priorityMessage = new PriorityMessage(message, priority);
            Broadcast(from, priorityMessage);
        }

        /// <summary>
        /// 获取同事的优先级（用于排序）
        /// </summary>
        private int GetColleaguePriority(IColleague colleague, object message)
        {
            // 这里可以根据同事类型或消息类型来确定处理优先级
            // 简单实现：如果消息是高优先级，则优先处理
            if (message is PriorityMessage priorityMessage)
            {
                return -(int)priorityMessage.Priority;
            }
            return 0;
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

            // 按优先级排序处理同事
            var sortedColleagues = colleaguesCopy.OrderBy(c => GetColleaguePriority(c, message)).ToArray();

            var tasks = new List<Task>();

            foreach (var colleague in sortedColleagues)
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
        /// 异步按优先级广播消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息内容</param>
        /// <param name="priority">消息优先级</param>
        /// <returns>表示异步操作的任务</returns>
        public async Task BroadcastWithPriorityAsync(string from, object message, MessagePriority priority)
        {
            if (_disposed)
                return;

            var priorityMessage = new PriorityMessage(message, priority);
            await BroadcastAsync(from, priorityMessage);
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
    /// 消息路由器
    /// 根据消息内容将消息路由到不同的处理通道
    /// </summary>
    public class MessageRouter : IDisposable
    {
        private readonly Dictionary<string, IAdvancedMediator> _mediators;
        private readonly Func<PriorityMessage, string> _routingFunction;
        private bool _disposed = false;

        public MessageRouter(Func<PriorityMessage, string> routingFunction)
        {
            _mediators = new Dictionary<string, IAdvancedMediator>();
            _routingFunction = routingFunction ?? throw new ArgumentNullException(nameof(routingFunction));
        }

        /// <summary>
        /// 注册消息通道
        /// </summary>
        /// <param name="channelName">通道名称</param>
        /// <param name="mediator">中介者</param>
        public void RegisterChannel(string channelName, IAdvancedMediator mediator)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MessageRouter));

            if (string.IsNullOrEmpty(channelName))
                throw new ArgumentException("通道名称不能为空", nameof(channelName));

            if (mediator == null)
                throw new ArgumentNullException(nameof(mediator));

            _mediators[channelName] = mediator;
        }

        /// <summary>
        /// 路由并发送消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息数据</param>
        public void RouteAndSend(string from, string to, PriorityMessage message)
        {
            if (_disposed)
                return;

            var channelName = _routingFunction(message);
            if (!string.IsNullOrEmpty(channelName) && _mediators.TryGetValue(channelName, out var mediator))
            {
                mediator.Send(from, to, message);
            }
        }

        /// <summary>
        /// 异步路由并发送消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="to">接收者标识</param>
        /// <param name="message">消息数据</param>
        public async Task RouteAndSendAsync(string from, string to, PriorityMessage message)
        {
            if (_disposed)
                return;

            var channelName = _routingFunction(message);
            if (!string.IsNullOrEmpty(channelName) && _mediators.TryGetValue(channelName, out var mediator))
            {
                await mediator.SendAsync(from, to, message);
            }
        }

        /// <summary>
        /// 路由并广播消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息数据</param>
        public void RouteAndBroadcast(string from, PriorityMessage message)
        {
            if (_disposed)
                return;

            var channelName = _routingFunction(message);
            if (!string.IsNullOrEmpty(channelName) && _mediators.TryGetValue(channelName, out var mediator))
            {
                mediator.Broadcast(from, message);
            }
        }

        /// <summary>
        /// 异步路由并广播消息
        /// </summary>
        /// <param name="from">发送者标识</param>
        /// <param name="message">消息数据</param>
        public async Task RouteAndBroadcastAsync(string from, PriorityMessage message)
        {
            if (_disposed)
                return;

            var channelName = _routingFunction(message);
            if (!string.IsNullOrEmpty(channelName) && _mediators.TryGetValue(channelName, out var mediator))
            {
                await mediator.BroadcastAsync(from, message);
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
                    // 释放所有中介者
                    foreach (var mediator in _mediators.Values)
                    {
                        mediator?.Dispose();
                    }
                    _mediators.Clear();
                }

                _disposed = true;
            }
        }
    }
}