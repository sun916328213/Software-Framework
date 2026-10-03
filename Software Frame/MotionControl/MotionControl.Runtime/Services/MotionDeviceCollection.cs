// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：MotionDeviceCollection。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections。导入不代表所有类型都会在运行时加载。
using System.Collections;

namespace MotionControl.Runtime.Services
{
    /// <summary>
    /// 保持插入顺序的线程安全设备集合。
    /// <para>枚举时使用快照，不会在设备I/O期间占用集合锁。</para>
    /// </summary>
    public sealed class MotionDeviceCollection<T> : IList<T>, IReadOnlyList<T>, IDisposable
    {
        private readonly object _sync = new();
        private readonly List<T> _items = new();
        private bool _disposed;

        public T this[int index]
        {
            get
            {
                lock (_sync)
                    return _items[index];
            }
            set
            {
                lock (_sync)
                {
                    ThrowIfDisposed();
                    _items[index] = value;
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_sync)
                    return _items.Count;
            }
        }

        public bool IsReadOnly => false;

        /// <summary>
        /// 执行添加操作；对应入口为 Add。
        /// 正文中的关键协作调用包括 ThrowIfDisposed、Add；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void Add(T item)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _items.Add(item);
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Clear。
        /// 正文中的关键协作调用包括 ThrowIfDisposed、Clear；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void Clear()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _items.Clear();
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Contains。
        /// 正文中的关键协作调用包括 Contains；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public bool Contains(T item)
        {
            lock (_sync)
                return _items.Contains(item);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CopyTo。
        /// 正文中的关键协作调用包括 CopyTo；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (_sync)
                _items.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetEnumerator。
        /// 正文中的关键协作调用包括 GetEnumerator、Snapshot；具体条件和先后次序见方法体。
        /// </summary>
        public IEnumerator<T> GetEnumerator() => Snapshot().GetEnumerator();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 IndexOf。
        /// 正文中的关键协作调用包括 IndexOf；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public int IndexOf(T item)
        {
            lock (_sync)
                return _items.IndexOf(item);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Insert。
        /// 正文中的关键协作调用包括 ThrowIfDisposed、Insert；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void Insert(int index, T item)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _items.Insert(index, item);
            }
        }

        /// <summary>
        /// 执行移除操作；对应入口为 Remove。
        /// 正文中的关键协作调用包括 ThrowIfDisposed、Remove；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public bool Remove(T item)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                return _items.Remove(item);
            }
        }

        /// <summary>
        /// 执行移除操作；对应入口为 RemoveAt。
        /// 正文中的关键协作调用包括 ThrowIfDisposed、RemoveAt；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void RemoveAt(int index)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _items.RemoveAt(index);
            }
        }

        /// <summary>返回当前内容的有序快照。</summary>
        public List<T> Snapshot()
        {
            lock (_sync)
                return new List<T>(_items);
        }

        /// <summary>原子替换全部内容，并返回替换前的有序快照。</summary>
        internal List<T> ReplaceWith(IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            var replacement = items.ToList();
            lock (_sync)
            {
                ThrowIfDisposed();
                var previous = new List<T>(_items);
                _items.Clear();
                _items.AddRange(replacement);
                return previous;
            }
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetEnumerator。
        /// 正文中的关键协作调用包括 GetEnumerator；具体条件和先后次序见方法体。
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// 执行资源释放步骤；对应入口为 DisposeAndSnapshot。
        /// 正文中的关键协作调用包括 Clear；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        internal List<T> DisposeAndSnapshot()
        {
            lock (_sync)
            {
                if (_disposed)
                    return new List<T>();

                _disposed = true;
                var previous = new List<T>(_items);
                _items.Clear();
                return previous;
            }
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 DisposeAndSnapshot；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => DisposeAndSnapshot();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ThrowIfDisposed。
        /// 正文中的关键协作调用包括 ThrowIf；具体条件和先后次序见方法体。
        /// </summary>
        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    }
}


