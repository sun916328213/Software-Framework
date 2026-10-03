// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：RangeObservableCollection、SuppressionScope。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.ObjectModel、System.Collections.Specialized、System.ComponentModel。导入不代表所有类型都会在运行时加载。
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Extension.Core.Collections
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

        /// <summary>
        /// 初始化 RangeObservableCollection 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public RangeObservableCollection() { }

        /// <summary>
        /// 初始化 RangeObservableCollection 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
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

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnCollectionChanged。
        /// 正文中的关键协作调用包括 OnCollectionChanged；具体条件和先后次序见方法体。
        /// </summary>
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (IsSuppressing)
            {
                _hasChanges = true;
                return;
            }

            base.OnCollectionChanged(e);
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnPropertyChanged。
        /// 正文中的关键协作调用包括 OnPropertyChanged；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// SuppressionScope 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
        /// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
        /// </summary>
        private sealed class SuppressionScope(RangeObservableCollection<T> owner) : IDisposable
        {
            /// <summary>
            /// 执行资源释放步骤；对应入口为 Dispose。
            /// 正文中的关键协作调用包括 EndSuppress；具体条件和先后次序见方法体。
            /// </summary>
            public void Dispose() => owner.EndSuppress();
        }
    }

}
