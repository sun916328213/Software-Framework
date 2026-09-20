using Extension.Core.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Extension.Patterns.Messaging.Observer.Advanced
{
    /// <summary>
    /// 事件参数基类
    /// </summary>
    public class EventArgsBase : EventArgs
    {
        public DateTime EventTime { get; }

        public EventArgsBase()
        {
            EventTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 泛型事件参数类
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    public class EventArgs<T> : EventArgsBase
    {
        public T Data { get; }

        public EventArgs(T data)
        {
            Data = data;
        }
    }

    /// <summary>
    /// 高级观察者接口
    /// 支持事件驱动的通知机制
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    public interface IAdvancedObserver<T>
    {
        /// <summary>
        /// 处理事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        void OnEvent(object sender, EventArgs<T> e);
    }

    /// <summary>
    /// 高级被观察者接口
    /// 支持事件订阅和发布机制
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    public interface IAdvancedObservable<T> : IDisposable
    {
        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <param name="observer">观察者</param>
        void Subscribe(IAdvancedObserver<T> observer);

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        /// <param name="observer">观察者</param>
        void Unsubscribe(IAdvancedObserver<T> observer);

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <param name="data">事件数据</param>
        void Publish(T data);

        /// <summary>
        /// 异步发布事件
        /// </summary>
        /// <param name="data">事件数据</param>
        /// <returns>表示异步操作的任务</returns>
        Task PublishAsync(T data);
    }

    /// <summary>
    /// 高级观察者基类
    /// 提供事件处理的基础实现
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    public abstract class AdvancedObserverBase<T> : IAdvancedObserver<T>
    {
        /// <summary>
        /// 处理事件的抽象方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        public abstract void HandleEvent(object sender, EventArgs<T> e);

        /// <summary>
        /// 实现OnEvent方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        public void OnEvent(object sender, EventArgs<T> e)
        {
            HandleEvent(sender, e);
        }
    }

    /// <summary>
    /// 高级被观察者基类
    /// 提供事件订阅和发布的完整实现
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    public class AdvancedObservable<T> : IAdvancedObservable<T>
    {
        private readonly ConcurrentList<IAdvancedObserver<T>> _observers = new ConcurrentList<IAdvancedObserver<T>>();
        private bool _disposed = false;

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <param name="observer">观察者</param>
        public void Subscribe(IAdvancedObserver<T> observer)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedObservable<T>));

            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
            }
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        /// <param name="observer">观察者</param>
        public void Unsubscribe(IAdvancedObserver<T> observer)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedObservable<T>));

            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            _observers.Remove(observer);
        }

        /// <summary>
        /// 批量订阅事件
        /// </summary>
        /// <param name="observers">观察者集合</param>
        public void SubscribeRange(IEnumerable<IAdvancedObserver<T>> observers)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedObservable<T>));

            if (observers == null)
                throw new ArgumentNullException(nameof(observers));

            foreach (var observer in observers)
            {
                if (observer != null && !_observers.Contains(observer))
                {
                    _observers.Add(observer);
                }
            }
        }

        /// <summary>
        /// 批量取消订阅事件
        /// </summary>
        /// <param name="observers">观察者集合</param>
        public void UnsubscribeRange(IEnumerable<IAdvancedObserver<T>> observers)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedObservable<T>));

            if (observers == null)
                throw new ArgumentNullException(nameof(observers));

            foreach (var observer in observers)
            {
                if (observer != null)
                {
                    _observers.Remove(observer);
                }
            }
        }

        /// <summary>
        /// 清空所有订阅者
        /// </summary>
        public void ClearSubscribers()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedObservable<T>));

            _observers.Clear();
        }

        /// <summary>
        /// 检查是否包含指定订阅者
        /// </summary>
        /// <param name="observer">要检查的订阅者</param>
        /// <returns>如果包含返回true，否则返回false</returns>
        public bool ContainsSubscriber(IAdvancedObserver<T> observer)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdvancedObservable<T>));

            if (observer == null)
                return false;

            return _observers.Contains(observer);
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <param name="data">事件数据</param>
        public void Publish(T data)
        {
            if (_disposed)
                return;

            // 创建订阅者列表的快照以避免在迭代时修改集合
            int count = _observers.Count;
            if (count == 0)
                return;

            IAdvancedObserver<T>[] observersCopy = new IAdvancedObserver<T>[count];
            _observers.CopyTo(observersCopy, 0);

            var eventArgs = new EventArgs<T>(data);
            
            foreach (var observer in observersCopy)
            {
                if (observer == null)
                    continue;

                try
                {
                    observer.OnEvent(this, eventArgs);
                }
                catch (Exception ex)
                {
                    // 记录异常但不中断其他订阅者的处理
                    Console.WriteLine($"处理事件时发生异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 异步发布事件
        /// </summary>
        /// <param name="data">事件数据</param>
        public async Task PublishAsync(T data)
        {
            if (_disposed)
                return;

            // 异步执行发布操作
            await Task.Run(() => Publish(data)).ConfigureAwait(false);
        }

        /// <summary>
        /// 获取订阅者数量
        /// </summary>
        public int SubscriberCount => _observers.Count;

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
                    _observers.Clear();
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 基于委托的观察者实现
    /// 提供更灵活的事件处理方式
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    public class DelegateObserver<T> : IAdvancedObserver<T>
    {
        private readonly Action<object, EventArgs<T>> _handler;

        /// <summary>
        /// 获取观察者名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">观察者名称</param>
        /// <param name="handler">事件处理委托</param>
        public DelegateObserver(string name, Action<object, EventArgs<T>> handler)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// 处理事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        public void OnEvent(object sender, EventArgs<T> e)
        {
            _handler?.Invoke(sender, e);
        }
    }
}