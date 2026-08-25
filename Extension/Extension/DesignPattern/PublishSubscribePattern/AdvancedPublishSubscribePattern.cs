using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Extension.DesignPattern.PublishSubscribePattern.Advanced
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
    /// <typeparam name="T">消息数据类型</typeparam>
    public class PriorityMessage<T>
    {
        public T Data { get; }
        public MessagePriority Priority { get; }
        public DateTime CreationTime { get; }
        public string Topic { get; }

        public PriorityMessage(T data, MessagePriority priority, string topic = null)
        {
            Data = data;
            Priority = priority;
            CreationTime = DateTime.Now;
            Topic = topic;
        }
    }

    /// <summary>
    /// 消息过滤器接口
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public interface IMessageFilter<T>
    {
        /// <summary>
        /// 判断消息是否应该被处理
        /// </summary>
        /// <param name="message">消息数据</param>
        /// <returns>如果应该处理返回true，否则返回false</returns>
        bool ShouldProcess(T message);
    }

    /// <summary>
    /// 带过滤器的订阅者
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public class FilteredSubscriber<T> : ISubscriber<T>
    {
        private readonly ISubscriber<T> _innerSubscriber;
        private readonly IMessageFilter<T> _filter;
        private bool _disposed = false;

        public string Name => _innerSubscriber is SubscriberBase<T> baseSubscriber ? baseSubscriber.Name : "FilteredSubscriber";

        public FilteredSubscriber(ISubscriber<T> innerSubscriber, IMessageFilter<T> filter)
        {
            _innerSubscriber = innerSubscriber ?? throw new ArgumentNullException(nameof(innerSubscriber));
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        }

        public void HandleMessage(T message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FilteredSubscriber<T>));

            if (_filter.ShouldProcess(message))
            {
                _innerSubscriber.HandleMessage(message);
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
                    if (_innerSubscriber is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 主题过滤器
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public class TopicFilter<T> : IMessageFilter<PriorityMessage<T>>
    {
        private readonly HashSet<string> _allowedTopics;

        public TopicFilter(IEnumerable<string> allowedTopics)
        {
            _allowedTopics = new HashSet<string>(allowedTopics ?? throw new ArgumentNullException(nameof(allowedTopics)));
        }

        public bool ShouldProcess(PriorityMessage<T> message)
        {
            if (string.IsNullOrEmpty(message.Topic))
                return _allowedTopics.Contains(string.Empty) || _allowedTopics.Count == 0;

            return _allowedTopics.Contains(message.Topic);
        }
    }

    /// <summary>
    /// 优先级过滤器
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public class PriorityFilter<T> : IMessageFilter<PriorityMessage<T>>
    {
        private readonly MessagePriority _minPriority;

        public PriorityFilter(MessagePriority minPriority)
        {
            _minPriority = minPriority;
        }

        public bool ShouldProcess(PriorityMessage<T> message)
        {
            return message.Priority >= _minPriority;
        }
    }

    /// <summary>
    /// 组合过滤器
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public class CompositeFilter<T> : IMessageFilter<T>
    {
        private readonly List<IMessageFilter<T>> _filters;

        public CompositeFilter(IEnumerable<IMessageFilter<T>> filters)
        {
            _filters = new List<IMessageFilter<T>>(filters ?? throw new ArgumentNullException(nameof(filters)));
        }

        public bool ShouldProcess(T message)
        {
            return _filters.All(filter => filter.ShouldProcess(message));
        }
    }

    /// <summary>
    /// 高级消息中心实现
    /// 支持优先级处理、消息过滤等功能
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public class AdvancedMessageBroker<T> : IMessageBroker<PriorityMessage<T>>
    {
        private readonly ConcurrentDictionary<string, ConcurrentBag<ISubscriber<PriorityMessage<T>>>> _topicSubscribers =
            new ConcurrentDictionary<string, ConcurrentBag<ISubscriber<PriorityMessage<T>>>>();
        private readonly ConcurrentBag<ISubscriber<PriorityMessage<T>>> _allSubscribers = new ConcurrentBag<ISubscriber<PriorityMessage<T>>>();
        private readonly object _lock = new object();
        private bool _disposed = false;

        /// <summary>
        /// 获取订阅者数量
        /// </summary>
        public int SubscriberCount => _allSubscribers.Count;

        /// <summary>
        /// 订阅所有消息
        /// </summary>
        /// <param name="subscriber">订阅者</param>
        public void Subscribe(ISubscriber<PriorityMessage<T>> subscriber)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedMessageBroker<T>));

            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));

            if (!_allSubscribers.Contains(subscriber))
            {
                _allSubscribers.Add(subscriber);
            }
        }

        /// <summary>
        /// 订阅特定主题的消息
        /// </summary>
        /// <param name="topic">主题</param>
        /// <param name="subscriber">订阅者</param>
        public void SubscribeToTopic(string topic, ISubscriber<PriorityMessage<T>> subscriber)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedMessageBroker<T>));

            if (string.IsNullOrEmpty(topic))
                throw new ArgumentException("主题不能为空", nameof(topic));

            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));

            var subscribers = _topicSubscribers.GetOrAdd(topic, _ => new ConcurrentBag<ISubscriber<PriorityMessage<T>>>());
            if (!subscribers.Contains(subscriber))
            {
                subscribers.Add(subscriber);
            }
        }

        /// <summary>
        /// 取消订阅所有消息
        /// </summary>
        /// <param name="subscriber">订阅者</param>
        public void Unsubscribe(ISubscriber<PriorityMessage<T>> subscriber)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedMessageBroker<T>));

            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));

            // 从所有订阅者中移除
            RemoveSubscriberFromBag(_allSubscribers, subscriber);

            // 从所有主题中移除
            foreach (var topic in _topicSubscribers.Keys)
            {
                if (_topicSubscribers.TryGetValue(topic, out var subscribers))
                {
                    RemoveSubscriberFromBag(subscribers, subscriber);
                }
            }
        }

        /// <summary>
        /// 取消订阅特定主题的消息
        /// </summary>
        /// <param name="topic">主题</param>
        /// <param name="subscriber">订阅者</param>
        public void UnsubscribeFromTopic(string topic, ISubscriber<PriorityMessage<T>> subscriber)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedMessageBroker<T>));

            if (string.IsNullOrEmpty(topic))
                throw new ArgumentException("主题不能为空", nameof(topic));

            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));

            if (_topicSubscribers.TryGetValue(topic, out var subscribers))
            {
                RemoveSubscriberFromBag(subscribers, subscriber);
            }
        }

        /// <summary>
        /// 从ConcurrentBag中移除订阅者
        /// </summary>
        private void RemoveSubscriberFromBag(ConcurrentBag<ISubscriber<PriorityMessage<T>>> bag, ISubscriber<PriorityMessage<T>> subscriber)
        {
            lock (_lock)
            {
                var subscribersList = bag.ToList();
                subscribersList.Remove(subscriber);
                // 清空原集合
                while (bag.TryTake(out _)) { }
                // 重新添加剩余的订阅者
                foreach (var sub in subscribersList)
                {
                    bag.Add(sub);
                }
            }
        }

        /// <summary>
        /// 批量订阅消息
        /// </summary>
        /// <param name="subscribers">订阅者集合</param>
        public void SubscribeRange(IEnumerable<ISubscriber<PriorityMessage<T>>> subscribers)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedMessageBroker<T>));

            if (subscribers == null)
                throw new ArgumentNullException(nameof(subscribers));

            foreach (var subscriber in subscribers)
            {
                if (subscriber != null)
                {
                    Subscribe(subscriber);
                }
            }
        }

        /// <summary>
        /// 批量取消订阅消息
        /// </summary>
        /// <param name="subscribers">订阅者集合</param>
        public void UnsubscribeRange(IEnumerable<ISubscriber<PriorityMessage<T>>> subscribers)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedMessageBroker<T>));

            if (subscribers == null)
                throw new ArgumentNullException(nameof(subscribers));

            foreach (var subscriber in subscribers)
            {
                if (subscriber != null)
                {
                    Unsubscribe(subscriber);
                }
            }
        }

        /// <summary>
        /// 清空所有订阅者
        /// </summary>
        public void ClearSubscribers()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedMessageBroker<T>));

            lock (_lock)
            {
                while (_allSubscribers.TryTake(out _)) { }
                _topicSubscribers.Clear();
            }
        }

        /// <summary>
        /// 发布消息给所有订阅者（按优先级排序）
        /// </summary>
        /// <param name="message">消息数据</param>
        public void Publish(PriorityMessage<T> message)
        {
            if (_disposed)
                return;

            // 创建订阅者列表的快照以避免在迭代时修改集合
            ISubscriber<PriorityMessage<T>>[] subscribersCopy;
            lock (_lock)
            {
                subscribersCopy = _allSubscribers.OrderBy(s => GetSubscriberPriority(s, message)).ToArray();
            }

            // 按优先级顺序处理订阅者
            foreach (var subscriber in subscribersCopy)
            {
                if (subscriber == null)
                    continue;

                try
                {
                    subscriber.HandleMessage(message);
                }
                catch (Exception ex)
                {
                    // 记录异常但不中断其他订阅者的处理
                    Console.WriteLine($"处理消息时发生异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 发布消息给特定主题的订阅者
        /// </summary>
        /// <param name="topic">主题</param>
        /// <param name="message">消息数据</param>
        public void PublishToTopic(string topic, PriorityMessage<T> message)
        {
            if (_disposed)
                return;

            if (string.IsNullOrEmpty(topic))
                throw new ArgumentException("主题不能为空", nameof(topic));

            if (_topicSubscribers.TryGetValue(topic, out var subscribers))
            {
                // 创建订阅者列表的快照以避免在迭代时修改集合
                ISubscriber<PriorityMessage<T>>[] subscribersCopy;
                lock (_lock)
                {
                    subscribersCopy = subscribers.OrderBy(s => GetSubscriberPriority(s, message)).ToArray();
                }

                // 按优先级顺序处理订阅者
                foreach (var subscriber in subscribersCopy)
                {
                    if (subscriber == null)
                        continue;

                    try
                    {
                        subscriber.HandleMessage(message);
                    }
                    catch (Exception ex)
                    {
                        // 记录异常但不中断其他订阅者的处理
                        Console.WriteLine($"处理消息时发生异常: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 获取订阅者的优先级（用于排序）
        /// </summary>
        private int GetSubscriberPriority(ISubscriber<PriorityMessage<T>> subscriber, PriorityMessage<T> message)
        {
            // 这里可以根据订阅者类型或其它条件来确定优先级
            // 简单实现：高优先级消息优先处理
            return -(int)message.Priority;
        }

        /// <summary>
        /// 异步发布消息给所有订阅者
        /// </summary>
        /// <param name="message">消息数据</param>
        public async Task PublishAsync(PriorityMessage<T> message)
        {
            if (_disposed)
                return;

            // 异步执行发布操作
            await Task.Run(() => Publish(message)).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步发布消息给特定主题的订阅者
        /// </summary>
        /// <param name="topic">主题</param>
        /// <param name="message">消息数据</param>
        public async Task PublishToTopicAsync(string topic, PriorityMessage<T> message)
        {
            if (_disposed)
                return;

            if (string.IsNullOrEmpty(topic))
                throw new ArgumentException("主题不能为空", nameof(topic));

            // 异步执行发布操作
            await Task.Run(() => PublishToTopic(topic, message)).ConfigureAwait(false);
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
                    // 清空订阅者列表
                    ClearSubscribers();
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 消息路由器
    /// 根据消息内容将消息路由到不同的处理通道
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public class MessageRouter<T> : IDisposable
    {
        private readonly Dictionary<string, IMessageBroker<PriorityMessage<T>>> _brokers;
        private readonly Func<PriorityMessage<T>, string> _routingFunction;
        private bool _disposed = false;

        public MessageRouter(Func<PriorityMessage<T>, string> routingFunction)
        {
            _brokers = new Dictionary<string, IMessageBroker<PriorityMessage<T>>>();
            _routingFunction = routingFunction ?? throw new ArgumentNullException(nameof(routingFunction));
        }

        /// <summary>
        /// 注册消息通道
        /// </summary>
        /// <param name="channelName">通道名称</param>
        /// <param name="broker">消息中心</param>
        public void RegisterChannel(string channelName, IMessageBroker<PriorityMessage<T>> broker)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MessageRouter<T>));

            if (string.IsNullOrEmpty(channelName))
                throw new ArgumentException("通道名称不能为空", nameof(channelName));

            if (broker == null)
                throw new ArgumentNullException(nameof(broker));

            _brokers[channelName] = broker;
        }

        /// <summary>
        /// 路由并发布消息
        /// </summary>
        /// <param name="message">消息数据</param>
        public void RouteAndPublish(PriorityMessage<T> message)
        {
            if (_disposed)
                return;

            var channelName = _routingFunction(message);
            if (!string.IsNullOrEmpty(channelName) && _brokers.TryGetValue(channelName, out var broker))
            {
                broker.Publish(message);
            }
        }

        /// <summary>
        /// 异步路由并发布消息
        /// </summary>
        /// <param name="message">消息数据</param>
        public async Task RouteAndPublishAsync(PriorityMessage<T> message)
        {
            if (_disposed)
                return;

            var channelName = _routingFunction(message);
            if (!string.IsNullOrEmpty(channelName) && _brokers.TryGetValue(channelName, out var broker))
            {
                await broker.PublishAsync(message).ConfigureAwait(false);
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
                    // 释放所有消息中心
                    foreach (var broker in _brokers.Values)
                    {
                        broker?.Dispose();
                    }
                    _brokers.Clear();
                }

                _disposed = true;
            }
        }
    }
}