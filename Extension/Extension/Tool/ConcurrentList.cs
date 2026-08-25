using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Extension.Tool
{
    /// <summary>
    /// 线程安全的列表实现
    /// 提供类似List的接口，具有线程安全特性
    /// </summary>
    /// <typeparam name="T">列表元素类型</typeparam>
    public class ConcurrentList<T> : IList<T>, IReadOnlyList<T>, IDisposable
    {
        private readonly List<T> _list = new List<T>();
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        /// <summary>
        /// 初始化 ConcurrentList 类的新实例，包含指定集合的元素
        /// </summary>
        /// <param name="collection">用于初始化列表的集合</param>
        public ConcurrentList(IEnumerable<T> collection)
        {
            if (collection != null)
            {
                lock (_lockObject)
                {
                    if (!_disposed)
                    {
                        _list = new List<T>(collection);
                    }
                }
            }
        }

        /// <summary>
        /// 初始化 ConcurrentList 类的新实例
        /// </summary>
        public ConcurrentList()
        {
            _list = new List<T>();
        }

        /// <summary>
        /// 获取或设置指定索引处的元素
        /// </summary>
        /// <param name="index">索引</param>
        /// <returns>指定索引处的元素</returns>
        public T this[int index]
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ConcurrentList<T>));

                lock (_lockObject)
                {
                    if (index >= 0 && index < _list.Count)
                    {
                        return _list[index];
                    }
                    return default;
                }
            }
            set
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ConcurrentList<T>));

                lock (_lockObject)
                {
                    if (index >= 0 && index < _list.Count)
                    {
                        _list[index] = value;
                    }
                }
            }
        }

        /// <summary>
        /// 获取列表中实际包含的元素数
        /// </summary>
        public int Count
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ConcurrentList<T>));

                lock (_lockObject)
                {
                    return _list.Count;
                }
            }
        }

        /// <summary>
        /// 获取一个值，该值指示列表是否为只读
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// 将元素添加到列表的末尾
        /// </summary>
        /// <param name="item">要添加到列表末尾的对象</param>
        public void Add(T item)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConcurrentList<T>));

            lock (_lockObject)
            {
                if (!_disposed)
                {
                    _list.Add(item);
                }
            }
        }



        /// <summary>
        /// 从列表中移除所有元素
        /// </summary>
        public void Clear()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConcurrentList<T>));

            lock (_lockObject)
            {
                _list.Clear();
            }
        }

        /// <summary>
        /// 确定某元素是否在列表中
        /// </summary>
        /// <param name="item">要在列表中定位的对象</param>
        /// <returns>如果在列表中找到item，则为true，否则为false</returns>
        public bool Contains(T item)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConcurrentList<T>));

            lock (_lockObject)
            {
                return _list.Contains(item);
            }
        }

        /// <summary>
        /// 将整个列表复制到兼容的一维数组中
        /// </summary>
        /// <param name="array">作为从列表复制的元素的目标位置的一维数组</param>
        /// <param name="arrayIndex">array中从零开始的索引，从此处开始复制</param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConcurrentList<T>));

            lock (_lockObject)
            {
                _list.CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        /// 返回循环访问列表的枚举数
        /// </summary>
        /// <returns>用于列表的枚举数</returns>
        public IEnumerator<T> GetEnumerator()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConcurrentList<T>));

            lock (_lockObject)
            {
                // 创建副本以避免在枚举过程中修改列表
                var listCopy = new List<T>(_list);
                foreach (var value in listCopy)
                {
                    yield return value;
                }
            }
        }

        /// <summary>
        /// 返回循环访问列表的枚举数
        /// </summary>
        /// <returns>用于列表的枚举数</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 搜索指定的对象，并返回整个列表中第一个匹配项的从零开始的索引
        /// </summary>
        /// <param name="item">要在列表中定位的对象</param>
        /// <returns>如果在整个列表中找到item的第一个匹配项，则为该项的从零开始的索引；否则为-1</returns>
        public int IndexOf(T item)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConcurrentList<T>));

            lock (_lockObject)
            {
                return _list.IndexOf(item);
            }
        }

        /// <summary>
        /// 将元素插入列表的指定位置
        /// </summary>
        /// <param name="index">从零开始的索引，应在该位置插入item</param>
        /// <param name="item">要插入的对象</param>
        public void Insert(int index, T item)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConcurrentList<T>));

            lock (_lockObject)
            {
                if (index < 0 || index > _list.Count)
                    throw new ArgumentOutOfRangeException(nameof(index), "索引超出范围");

                _list.Insert(index, item);
            }
        }

        /// <summary>
        /// 从列表中移除特定对象的第一个匹配项
        /// </summary>
        /// <param name="item">要从列表中移除的对象</param>
        /// <returns>如果成功移除item，则为true；否则为false</returns>
        public bool Remove(T item)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConcurrentList<T>));

            int index = IndexOf(item);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 移除列表中指定索引处的元素
        /// </summary>
        /// <param name="index">要移除的元素的从零开始的索引</param>
        public void RemoveAt(int index)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConcurrentList<T>));

            lock (_lockObject)
            {
                if (index < 0 || index >= _list.Count)
                    throw new ArgumentOutOfRangeException(nameof(index), "索引超出范围");

                _list.RemoveAt(index);
            }
        }



        /// <summary>
        /// 将指定集合的元素添加到列表末尾
        /// </summary>
        /// <param name="collection">要添加的元素集合</param>
        public void AddRange(IEnumerable<T> collection)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConcurrentList<T>));

            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            lock (_lockObject)
            {
                if (!_disposed)
                {
                    _list.AddRange(collection);
                }
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
                    _list.Clear();
                }

                _disposed = true;
            }
        }
       
    }
}