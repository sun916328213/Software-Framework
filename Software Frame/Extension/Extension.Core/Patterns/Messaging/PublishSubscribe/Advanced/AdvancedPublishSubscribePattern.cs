// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：MessagePriority、PriorityMessage、IMessageFilter、FilteredSubscriber、TopicFilter、PriorityFilter、CompositeFilter、AdvancedMessageBroker、MessageRouter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.Patterns.Messaging.PublishSubscribe、System、System.Collections.Concurrent、System.Collections.Generic、System.Linq、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using Extension.Patterns.Messaging.PublishSubscribe;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Extension.Patterns.Messaging.PublishSubscribe.Advanced
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

        /// <summary>
        /// 初始化 PriorityMessage 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
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

        /// <summary>
        /// 获取订阅者名称。
        /// </summary>
        /// <remarks>
        /// ⛔ 序 57（2026-09-18 修）：原实现是
        /// <c>_innerSubscriber is SubscriberBase&lt;T&gt; b ? b.Name : "FilteredSubscriber"</c>，
        /// 两个方向都不对：
        /// <list type="number">
        /// <item>内层**不是** <c>SubscriberBase&lt;T&gt;</c> 时（例如 <c>DelegateSubscriber&lt;T&gt;</c> ——
        /// 它同样有 <c>Name</c>，但**不**派生自 <c>SubscriberBase&lt;T&gt;</c>，两者是平行实现），
        /// 一律返回**固定字面量** "FilteredSubscriber" ⇒ 多个包装器名字**全一样**，日志里分不出谁是谁。</item>
        /// <item>内层**是** <c>SubscriberBase&lt;T&gt;</c> 时返回内层的**裸名字**，与内层本身**完全同名**
        /// ⇒ 反而看不出"这里套了一层包装"。</item>
        /// </list>
        /// 现在统一成 <c>Filtered(&lt;内层名&gt;)</c>，两个方向都能分辨。
        /// <para>⚠️ 只能在**具体类型**上取名字：<c>ISubscriber&lt;T&gt;</c> 上**没有** <c>Name</c>
        /// 成员（见 §5.30.5 第 52 条那一族的接口设计）⇒ 与序 43 的修法同思路，
        /// 按仓内两个具体类型分别取，都取不到时退回**运行时类型名**（仍比固定字面量有信息量）。</para>
        /// <para>✅ 下游影响：<c>Name</c> 在生产代码里**零调用点**（全仓 <c>Patterns/</c> 内唯一的 <c>.Name</c>
        /// 就是本行的定义本身），只有测试断言它。</para>
        /// </remarks>
        public string Name => $"Filtered({ResolveInnerName()})";

        /// <summary>
        /// 解析内层订阅者的显示名。
        /// </summary>
        /// <returns>内层名；两个仓内具体类型都匹配不上时返回其运行时类型名。</returns>
        private string ResolveInnerName()
        {
            switch (_innerSubscriber)
            {
                case SubscriberBase<T> baseSubscriber:
                    return baseSubscriber.Name;
                case DelegateSubscriber<T> delegateSubscriber:
                    return delegateSubscriber.Name;
                default:
                    return _innerSubscriber.GetType().Name;
            }
        }

        /// <summary>
        /// 初始化 FilteredSubscriber 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
        /// </summary>
        public FilteredSubscriber(ISubscriber<T> innerSubscriber, IMessageFilter<T> filter)
        {
            _innerSubscriber = innerSubscriber ?? throw new ArgumentNullException(nameof(innerSubscriber));
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 HandleMessage。
        /// 正文中的关键协作调用包括 nameof、ShouldProcess、HandleMessage；具体条件和先后次序见方法体。
        /// </summary>
        public void HandleMessage(T message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FilteredSubscriber<T>));

            if (_filter.ShouldProcess(message))
            {
                _innerSubscriber.HandleMessage(message);
            }
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Dispose、SuppressFinalize；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 保留原分支：第三方自定义的 `ISubscriber<T>` 若**声明了** `IDisposable`，仍会被释放。
                    if (_innerSubscriber is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    // ⛔ 序 43（2026-09-18 修）：`ISubscriber<T>`（`PublishSubscribePattern.cs:13`）
                    // **不继承** `IDisposable`，而仓内两个实现 —— `SubscriberBase<T>`（`:87`）/
                    // `DelegateSubscriber<T>`（`:138`）—— 都只有 `public void Dispose()`，
                    // **没有**声明 `: IDisposable` ⇒ 上面的 `is` 对它们**恒为 false**，
                    // 内层订阅者的 `Dispose()` 永远不会被调用（与 `FilteredColleague` **逐字同型**，
                    // 见 `AdvancedMediatorPattern.cs` 的同名分支）。
                    //
                    // 修法走**仓内闭环**：补"按具体类型释放"的分支，**不动** `ISubscriber<T>` 的
                    // 公开契约（让它继承 `IDisposable` 属破坏性变更：全仓所有实现类都要补成员）。
                    // ⚠️ 两个具体类型是**平行**的 —— `DelegateSubscriber<T>` **不**派生自
                    // `SubscriberBase<T>`，所以两个分支都要写，不能只写基类那个。
                    // ⚠️ 用 `else if` 串起来：避免将来出现"既声明 `IDisposable` 又是这两个类型"时重复 Dispose。
                    else if (_innerSubscriber is SubscriberBase<T> baseSubscriber)
                    {
                        baseSubscriber.Dispose();
                    }
                    else if (_innerSubscriber is DelegateSubscriber<T> delegateSubscriber)
                    {
                        delegateSubscriber.Dispose();
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
    /// <remarks>
    /// ⛔ 序 60（2026-09-18 补注释，**未改行为**）：<b>空 topic 既不是"全通过"也不是"全拦"</b>。
    /// <para>规则是"<b>白名单为空 或 含空串</b>才放行"：当 <c>PriorityMessage&lt;T&gt;.Topic</c>
    /// 为 <c>null</c> / <c>""</c> 时，只有白名单**为空**、或白名单**含空串**才放行；
    /// 白名单非空且不含空串 ⇒ **整条拦掉**。</para>
    /// <para>⚠️ 而 <c>PriorityMessage&lt;T&gt;</c> 的 topic **默认是 <c>null</c>**，
    /// <c>SendWithPriority</c> / <c>BroadcastWithPriority</c> 都不传它
    /// ⇒ 凡走这两个入口的消息，只要白名单非空且不含空串，就**全被丢弃**（静默，不报错）。
    /// 与 <c>MediatorPattern.Advanced.TypeFilter</c> 的 <c>MessageType</c> 坑同型。</para>
    /// </remarks>
    public class TopicFilter<T> : IMessageFilter<PriorityMessage<T>>
    {
        private readonly HashSet<string> _allowedTopics;

        /// <summary>
        /// 初始化 TopicFilter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
        /// </summary>
        public TopicFilter(IEnumerable<string> allowedTopics)
        {
            _allowedTopics = new HashSet<string>(allowedTopics ?? throw new ArgumentNullException(nameof(allowedTopics)));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ShouldProcess。
        /// 正文中的关键协作调用包括 IsNullOrEmpty、Contains；具体条件和先后次序见方法体。
        /// </summary>
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
    /// <remarks>
    /// ⛔ 序 60（2026-09-18 补注释，**未改行为**）：这是"<b>最低优先级</b>"语义，
    /// **不是精确匹配**。
    /// <para>判据是 <c>message.Priority &gt;= _minPriority</c>
    /// ⇒ 传 <c>MessagePriority.High</c> 时 <c>MessagePriority.Critical</c> **也会通过**。
    /// 取值：<c>Low=0</c> / <c>Normal=1</c> / <c>High=2</c> / <c>Critical=3</c>。</para>
    /// <para>⚠️ 同一语义在 <c>MediatorPattern.Advanced.PriorityFilter</c>（非泛型）里
    /// **逐字重复**，两处要一起看。</para>
    /// </remarks>
    public class PriorityFilter<T> : IMessageFilter<PriorityMessage<T>>
    {
        private readonly MessagePriority _minPriority;

        /// <summary>
        /// 初始化 PriorityFilter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public PriorityFilter(MessagePriority minPriority)
        {
            _minPriority = minPriority;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ShouldProcess。
        /// </summary>
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

        /// <summary>
        /// 初始化 CompositeFilter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
        /// </summary>
        public CompositeFilter(IEnumerable<IMessageFilter<T>> filters)
        {
            _filters = new List<IMessageFilter<T>>(filters ?? throw new ArgumentNullException(nameof(filters)));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ShouldProcess。
        /// 正文中的关键协作调用包括 All、ShouldProcess；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 初始化 MessageRouter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
        /// </summary>
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