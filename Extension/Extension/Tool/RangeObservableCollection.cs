using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Extension.Tool
{
    /// <summary>
    /// 支持 <see cref="AddRange"/> 批量添加的 ObservableCollection。
    /// 批量操作期间抑制 CollectionChanged 事件，结束后仅触发一次 Reset 通知，
    /// 避免 TabControl 等 ItemsControl 为每个 Add 执行独立的布局重算。
    /// </summary>
    public class RangeObservableCollection<T> : ObservableCollection<T>
    {
        private int _suppressionDepth;
        private bool _hasChanges;

        public RangeObservableCollection() { }

        public RangeObservableCollection(IEnumerable<T> collection) : base(collection) { }

        /// <summary>是否正在抑制通知。</summary>
        public bool IsSuppressing => _suppressionDepth > 0;

        /// <summary>
        /// 批量添加元素，期间抑制 CollectionChanged 通知，结束后触发一次 Reset。
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            using (SuppressNotifications())
            {
                foreach (var item in items)
                    Add(item);
            }
        }

        /// <summary>
        /// 批量移除元素，期间抑制 CollectionChanged 通知，结束后触发一次 Reset。
        /// </summary>
        public void RemoveRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            using (SuppressNotifications())
            {
                foreach (var item in items)
                    Remove(item);
            }
        }

        /// <summary>
        /// 替换整个集合内容（先 Clear 再 AddRange），仅触发一次 Reset。
        /// </summary>
        public void ReplaceAll(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            using (SuppressNotifications())
            {
                Clear();
                foreach (var item in items)
                    Add(item);
            }
        }

        /// <summary>获取一个抑制通知的 IDisposable，退出 using 块时自动触发 Reset。</summary>
        public IDisposable SuppressNotifications()
        {
            _suppressionDepth++;
            return new SuppressionScope(this);
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (IsSuppressing)
            {
                _hasChanges = true;
                return;
            }

            base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (IsSuppressing) return;
            base.OnPropertyChanged(e);
        }

        /// <summary>恢复通知并触发 Reset。</summary>
        private void EndSuppress()
        {
            _suppressionDepth--;
            if (_suppressionDepth == 0 && _hasChanges)
            {
                _hasChanges = false;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        private sealed class SuppressionScope(RangeObservableCollection<T> owner) : IDisposable
        {
            public void Dispose() => owner.EndSuppress();
        }
    }

}
