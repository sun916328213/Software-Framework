using DPLibrary_Base_F48.Tools;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DPLibrary_Base_F48
{
    /// <summary>
    /// 用于批量添加和移除元素的ObservableCollection
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BulkObservableCollection<T> : ObservableCollection<T>, IDisposable
    {
        bool mIsSupsend = false;

        // 内部锁对象
        private readonly object mAsyncLock = new object();

        public BulkObservableCollection()
        {
            // 线程安全锁
            BindingOperations.EnableCollectionSynchronization(this, mAsyncLock);
        }

        /// <summary>
        /// 是否屏蔽CollectionChanged事件的触发
        /// </summary>
        public bool IsSuspend
        {
            get
            {
                return mIsSupsend;
            }
            set
            {
                if (mIsSupsend != value)
                {
                    mIsSupsend = value;
                    if (!value)
                    {
                        this.RaiseReset();
                    }
                }
            }
        }

        private void RaiseReset()
        {
            base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void InsertItem(int index, T item)
        {
            lock (mAsyncLock)
            {
                base.InsertItem(index, item);
            }
        }

        protected override void ClearItems()
        {
            lock (mAsyncLock)
            {
                foreach (var item in this)
                {
                    if (item is IDisposable d)
                    {
                        d.Dispose();
                    }
                }
                base.ClearItems();
            }
        }

        protected override void RemoveItem(int index)
        {
            lock (mAsyncLock)
            {
                var item = this[index];
                if (item is IDisposable d)
                {
                    d.Dispose();
                }

                base.RemoveItem(index);
            }
        }

        protected override void SetItem(int index, T item)
        {
            lock (mAsyncLock)
            {
                var oldItem = this[index];
                if (oldItem is IDisposable d)
                {
                    d.Dispose();
                }
                base.SetItem(index, item);
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!mIsSupsend)
            {
                base.OnCollectionChanged(e);
            }
        }


        public void Dispose()
        {
            mIsSupsend = true;
            ClearItems();
            BindingOperations.DisableCollectionSynchronization(this);
        }

        public static explicit operator BulkObservableCollection<T>(BulkObservableCollection<IToolResult> v)
        {
            throw new NotImplementedException();
        }
    }
}
