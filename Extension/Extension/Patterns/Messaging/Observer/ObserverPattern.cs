using Extension.Core.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Extension.Patterns.Messaging.Observer
{
    /// <summary>
    /// 抽象观察者接口
    /// 定义观察者的更新方法，当被观察对象状态改变时调用
    /// </summary>
    public interface IObserver
    {
        /// <summary>
        /// 更新方法，当被观察对象状态改变时调用
        /// </summary>
        /// <param name="subject">被观察的对象</param>
        void Update(IObservable subject);
    }

    /// <summary>
    /// 抽象被观察者接口
    /// 定义被观察者的基本操作：添加、移除观察者和通知观察者
    /// </summary>
    public interface IObservable : IDisposable
    {
        /// <summary>
        /// 添加观察者
        /// </summary>
        /// <param name="observer">观察者</param>
        void Attach(IObserver observer);

        /// <summary>
        /// 移除观察者
        /// </summary>
        /// <param name="observer">观察者</param>
        void Detach(IObserver observer);

        /// <summary>
        /// 通知所有观察者
        /// </summary>
        void Notify();
    }

    /// <summary>
    /// 观察者模式基类
    /// 提供线程安全的观察者管理功能，使用ConcurrentList确保线程安全
    /// </summary>
    public abstract class ObservableBase : IObservable
    {
        private readonly ConcurrentList<IObserver> _observers = new ConcurrentList<IObserver>();
        private bool _disposed = false;

        /// <summary>
        /// 添加观察者
        /// </summary>
        /// <param name="observer">观察者</param>
        public void Attach(IObserver observer)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableBase));

            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            // 检查是否已存在该观察者
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
            }
        }

        /// <summary>
        /// 批量添加观察者
        /// 提高性能，避免多次调用单个Attach方法
        /// </summary>
        /// <param name="observers">观察者集合</param>
        public void AttachRange(IEnumerable<IObserver> observers)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableBase));

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
        /// 移除观察者
        /// </summary>
        /// <param name="observer">观察者</param>
        public void Detach(IObserver observer)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableBase));

            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            _observers.Remove(observer);
        }

        /// <summary>
        /// 批量移除观察者
        /// 提高性能，避免多次调用单个Detach方法
        /// </summary>
        /// <param name="observers">观察者集合</param>
        public void DetachRange(IEnumerable<IObserver> observers)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableBase));

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
        /// 清空所有观察者
        /// </summary>
        public void ClearObservers()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableBase));

            _observers.Clear();
        }

        /// <summary>
        /// 检查是否包含指定观察者
        /// </summary>
        /// <param name="observer">要检查的观察者</param>
        /// <returns>如果包含返回true，否则返回false</returns>
        public bool ContainsObserver(IObserver observer)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableBase));

            if (observer == null)
                return false;

            return _observers.Contains(observer);
        }

        /// <summary>
        /// 通知所有观察者
        /// </summary>
        public void Notify()
        {
            if (_disposed)
                return;

            // 优化：仅在有观察者时创建副本
            int count = _observers.Count;
            if (count == 0)
                return;

            // 创建副本以避免在遍历时修改集合
            IObserver[] observersCopy = new IObserver[count];
            _observers.CopyTo(observersCopy, 0);

            foreach (var observer in observersCopy)
            {
                if (observer == null)
                    continue;

                try
                {
                    observer.Update(this);
                }
                catch (Exception ex)
                {
                    // 记录详细异常信息但不中断其他观察者的处理
                    Console.WriteLine($"观察者处理更新时发生异常: {ex.Message}");
                    Console.WriteLine($"异常堆栈: {ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// 异步通知所有观察者
        /// </summary>
        /// <returns>表示异步操作的任务</returns>
        public async Task NotifyAsync()
        {
            if (_disposed)
                return;

            // 异步执行通知，避免阻塞调用线程
            await Task.Run(() => Notify()).ConfigureAwait(false);
        }

        /// <summary>
        /// 获取观察者数量
        /// </summary>
        public int ObserverCount => _observers.Count;

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
                    // 清空观察者列表并释放资源
                    _observers.Clear();
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 泛型观察者模式实现
    /// 可以传递特定类型的数据给观察者，提供更强的类型安全性
    /// </summary>
    /// <typeparam name="T">观察者接收的数据类型</typeparam>
    public class Observable<T> : ObservableBase
    {
        private T _data;
        private readonly object _lock = new object();
        private readonly bool _enableEqualityCheck;

        /// <summary>
        /// 默认构造函数
        /// 启用数据相等性检查，只有数据真正变化时才会通知观察者
        /// </summary>
        public Observable() : this(true)
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="enableEqualityCheck">是否启用数据相等性检查</param>
        public Observable(bool enableEqualityCheck)
        {
            _enableEqualityCheck = enableEqualityCheck;
        }

        /// <summary>
        /// 获取或设置数据，并在设置时通知所有观察者
        /// </summary>
        public T Data
        {
            get
            {
                lock (_lock)
                {
                    return _data;
                }
            }
            set
            {
                UpdateData(value);
            }
        }

        /// <summary>
        /// 更新数据并通知观察者
        /// </summary>
        /// <param name="newData">新数据</param>
        /// <returns>如果数据发生变化并触发了通知，返回true</returns>
        public bool UpdateData(T newData)
        {
            bool shouldNotify = false;

            lock (_lock)
            {
                // 根据配置决定是否进行相等性检查
                if (!_enableEqualityCheck)
                {
                    shouldNotify = true;
                }
                else
                {
                    // 检查数据是否真的发生变化
                    if (_data == null && newData != null)
                    {
                        shouldNotify = true;
                    }
                    else if (_data != null && newData == null)
                    {
                        shouldNotify = true;
                    }
                    else if (_data != null && !_data.Equals(newData))
                    {
                        shouldNotify = true;
                    }
                }

                _data = newData;
            }

            // 在锁外通知，避免死锁风险
            if (shouldNotify)
            {
                Notify();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 异步更新数据
        /// </summary>
        /// <param name="newData">新数据</param>
        /// <returns>表示异步操作的任务，任务结果指示数据是否发生变化</returns>
        public async Task<bool> UpdateDataAsync(T newData)
        {
            bool shouldNotify = false;

            lock (_lock)
            {
                // 与同步版本相同的逻辑
                if (!_enableEqualityCheck)
                {
                    shouldNotify = true;
                }
                else
                {
                    if (_data == null && newData != null)
                    {
                        shouldNotify = true;
                    }
                    else if (_data != null && newData == null)
                    {
                        shouldNotify = true;
                    }
                    else if (_data != null && !_data.Equals(newData))
                    {
                        shouldNotify = true;
                    }
                }

                _data = newData;
            }

            // 在锁外异步通知
            if (shouldNotify)
            {
                await NotifyAsync().ConfigureAwait(false);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 泛型观察者基类
    /// 提供类型安全的观察者实现，简化观察者的创建过程
    /// </summary>
    /// <typeparam name="T">观察的数据类型</typeparam>
    public abstract class Observer<T> : IObserver
    {
        /// <summary>
        /// 处理更新的抽象方法
        /// 子类需要实现此方法来处理接收到的通知和数据
        /// </summary>
        /// <param name="subject">被观察的对象</param>
        /// <param name="data">传递的数据</param>
        public abstract void HandleUpdate(IObservable subject, T data);

        /// <summary>
        /// 更新方法，当被观察对象状态改变时调用
        /// </summary>
        /// <param name="subject">被观察的对象</param>
        public void Update(IObservable subject)
        {
            if (subject == null)
                throw new ArgumentNullException(nameof(subject));

            if (subject is Observable<T> observable)
            {
                // 获取数据时使用锁保护
                T data;
                lock (observable)
                {
                    data = observable.Data;
                }
                HandleUpdate(subject, data);
            }
            else
            {
                // 添加日志记录不兼容的类型
                Console.WriteLine($"警告: 观察者类型不匹配 - 期望 Observable<{typeof(T).Name}>, 但收到 {subject.GetType().Name}");
            }
        }
    }
}