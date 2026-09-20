using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Extension.Patterns.Messaging.PublishSubscribe
{
    /// <summary>
    /// 消息订阅者接口
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public interface ISubscriber<T>
    {
        /// <summary>
        /// 处理消息
        /// </summary>
        /// <param name="message">消息数据</param>
        void HandleMessage(T message);
    }

    /// <summary>
    /// 消息发布者接口
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public interface IPublisher<T>
    {
        /// <summary>
        /// 发布消息
        /// </summary>
        /// <param name="message">消息数据</param>
        void Publish(T message);

        /// <summary>
        /// 异步发布消息
        /// </summary>
        /// <param name="message">消息数据</param>
        /// <returns>表示异步操作的任务</returns>
        Task PublishAsync(T message);
    }

    /// <summary>
    /// 消息中心接口
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public interface IMessageBroker<T> : IPublisher<T>, IDisposable
    {
        /// <summary>
        /// 订阅消息
        /// </summary>
        /// <param name="subscriber">订阅者</param>
        void Subscribe(ISubscriber<T> subscriber);

        /// <summary>
        /// 取消订阅消息
        /// </summary>
        /// <param name="subscriber">订阅者</param>
        void Unsubscribe(ISubscriber<T> subscriber);

        /// <summary>
        /// 批量订阅消息
        /// </summary>
        /// <param name="subscribers">订阅者集合</param>
        void SubscribeRange(IEnumerable<ISubscriber<T>> subscribers);

        /// <summary>
        /// 批量取消订阅消息
        /// </summary>
        /// <param name="subscribers">订阅者集合</param>
        void UnsubscribeRange(IEnumerable<ISubscriber<T>> subscribers);

        /// <summary>
        /// 清空所有订阅者
        /// </summary>
        void ClearSubscribers();

        /// <summary>
        /// 获取订阅者数量
        /// </summary>
        int SubscriberCount { get; }
    }

    /// <summary>
    /// 抽象订阅者基类
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public abstract class SubscriberBase<T> : ISubscriber<T>
    {
        private bool _disposed = false;

        /// <summary>
        /// 获取订阅者名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">订阅者名称</param>
        protected SubscriberBase(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// 处理消息
        /// 子类应重写此方法实现具体的事件处理逻辑
        /// </summary>
        /// <param name="message">消息数据</param>
        public abstract void HandleMessage(T message);

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
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 基于委托的订阅者实现
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public class DelegateSubscriber<T> : ISubscriber<T>
    {
        private readonly Action<T> _handler;
        private bool _disposed = false;

        /// <summary>
        /// 获取订阅者名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">订阅者名称</param>
        /// <param name="handler">消息处理委托</param>
        public DelegateSubscriber(string name, Action<T> handler)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// 处理消息
        /// 调用提供的委托处理消息数据
        /// </summary>
        /// <param name="message">消息数据</param>
        public void HandleMessage(T message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DelegateSubscriber<T>));

            if (_handler != null)
            {
                _handler(message);
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
    /// 消息中心实现
    /// 提供线程安全的消息发布和订阅功能
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public class MessageBroker<T> : IMessageBroker<T>
    {
        private readonly ConcurrentBag<ISubscriber<T>> _subscribers = new ConcurrentBag<ISubscriber<T>>();
        private readonly object _lock = new object();
        private bool _disposed = false;

        /// <summary>
        /// 获取订阅者数量
        /// </summary>
        public int SubscriberCount => _subscribers.Count;

        /// <summary>
        /// 订阅消息
        /// </summary>
        /// <param name="subscriber">订阅者</param>
        public void Subscribe(ISubscriber<T> subscriber)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MessageBroker<T>));

            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));

            // 检查是否已存在该订阅者
            if (!_subscribers.Contains(subscriber))
            {
                _subscribers.Add(subscriber);
            }
        }

        /// <summary>
        /// 取消订阅消息
        /// </summary>
        /// <param name="subscriber">订阅者</param>
        public void Unsubscribe(ISubscriber<T> subscriber)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MessageBroker<T>));

            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));

            // 注意：ConcurrentBag不支持直接移除元素，这里需要创建新的集合
            lock (_lock)
            {
                var subscribersList = _subscribers.ToList();
                subscribersList.Remove(subscriber);
                // 清空原集合
                while (_subscribers.TryTake(out _)) { }
                // 重新添加剩余的订阅者
                foreach (var sub in subscribersList)
                {
                    _subscribers.Add(sub);
                }
            }
        }

        /// <summary>
        /// 批量订阅消息
        /// </summary>
        /// <param name="subscribers">订阅者集合</param>
        public void SubscribeRange(IEnumerable<ISubscriber<T>> subscribers)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MessageBroker<T>));

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
        public void UnsubscribeRange(IEnumerable<ISubscriber<T>> subscribers)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MessageBroker<T>));

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
                throw new ObjectDisposedException(nameof(MessageBroker<T>));

            lock (_lock)
            {
                while (_subscribers.TryTake(out _)) { }
            }
        }

        /// <summary>
        /// 发布消息
        /// </summary>
        /// <param name="message">消息数据</param>
        public void Publish(T message)
        {
            if (_disposed)
                return;

            // 创建订阅者列表的快照以避免在迭代时修改集合
            ISubscriber<T>[] subscribersCopy;
            lock (_lock)
            {
                subscribersCopy = _subscribers.ToArray();
            }

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
        /// 异步发布消息
        /// </summary>
        /// <param name="message">消息数据</param>
        public async Task PublishAsync(T message)
        {
            if (_disposed)
                return;

            // 异步执行发布操作
            await Task.Run(() => Publish(message)).ConfigureAwait(false);
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
    /// 高级消息中心实现
    /// 提供更多高级功能，如主题订阅、消息过滤等
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    public class AdvancedMessageBroker<T> : IMessageBroker<T>
    {
        private readonly ConcurrentDictionary<string, ConcurrentBag<ISubscriber<T>>> _topicSubscribers = 
            new ConcurrentDictionary<string, ConcurrentBag<ISubscriber<T>>>();
        private readonly ConcurrentBag<ISubscriber<T>> _allSubscribers = new ConcurrentBag<ISubscriber<T>>();
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
        public void Subscribe(ISubscriber<T> subscriber)
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
        public void SubscribeToTopic(string topic, ISubscriber<T> subscriber)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedMessageBroker<T>));

            if (string.IsNullOrEmpty(topic))
                throw new ArgumentException("主题不能为空", nameof(topic));

            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));

            var subscribers = _topicSubscribers.GetOrAdd(topic, _ => new ConcurrentBag<ISubscriber<T>>());
            if (!subscribers.Contains(subscriber))
            {
                subscribers.Add(subscriber);
            }
        }

        /// <summary>
        /// 取消订阅所有消息
        /// </summary>
        /// <param name="subscriber">订阅者</param>
        public void Unsubscribe(ISubscriber<T> subscriber)
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
        public void UnsubscribeFromTopic(string topic, ISubscriber<T> subscriber)
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
        private void RemoveSubscriberFromBag(ConcurrentBag<ISubscriber<T>> bag, ISubscriber<T> subscriber)
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
        public void SubscribeRange(IEnumerable<ISubscriber<T>> subscribers)
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
        public void UnsubscribeRange(IEnumerable<ISubscriber<T>> subscribers)
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
        /// 发布消息给所有订阅者
        /// </summary>
        /// <param name="message">消息数据</param>
        public void Publish(T message)
        {
            if (_disposed)
                return;

            // 创建订阅者列表的快照以避免在迭代时修改集合
            ISubscriber<T>[] subscribersCopy;
            lock (_lock)
            {
                subscribersCopy = _allSubscribers.ToArray();
            }

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
        public void PublishToTopic(string topic, T message)
        {
            if (_disposed)
                return;

            if (string.IsNullOrEmpty(topic))
                throw new ArgumentException("主题不能为空", nameof(topic));

            if (_topicSubscribers.TryGetValue(topic, out var subscribers))
            {
                // 创建订阅者列表的快照以避免在迭代时修改集合
                ISubscriber<T>[] subscribersCopy;
                lock (_lock)
                {
                    subscribersCopy = subscribers.ToArray();
                }

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
        /// 异步发布消息给所有订阅者
        /// </summary>
        /// <param name="message">消息数据</param>
        public async Task PublishAsync(T message)
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
        public async Task PublishToTopicAsync(string topic, T message)
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
}