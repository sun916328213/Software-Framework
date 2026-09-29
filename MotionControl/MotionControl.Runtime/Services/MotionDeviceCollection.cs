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

        public void Add(T item)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _items.Add(item);
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _items.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (_sync)
                return _items.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (_sync)
                _items.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator() => Snapshot().GetEnumerator();

        public int IndexOf(T item)
        {
            lock (_sync)
                return _items.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _items.Insert(index, item);
            }
        }

        public bool Remove(T item)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                return _items.Remove(item);
            }
        }

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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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

        public void Dispose() => DisposeAndSnapshot();

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    }
}


