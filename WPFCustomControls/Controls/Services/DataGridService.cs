using Extension.Core.ControlEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// DataGrid服务类
    /// 负责处理DataGrid的数据操作逻辑
    /// 内部通过定时器批量刷新到UI，避免频繁跨线程调度
    /// </summary>
    public class DataGridService<T>
    {
        private readonly DispatcherTimer _batchTimer;
        private readonly ConcurrentQueue<T> _pendingItems = new ConcurrentQueue<T>();
        private int _retainNum = 200;

        public DataGridService()
        {
            Model = new DataGridModel<T>();
            _batchTimer = new DispatcherTimer(DispatcherPriority.Background);
            _batchTimer.Interval = TimeSpan.FromMilliseconds(100);
            _batchTimer.Tick += (s, e) => FlushBatch();
        }

        public DataGridModel<T> Model { get; }

        public virtual void Add(T data, int retainNum = 200)
        {
            _retainNum = retainNum;
            _pendingItems.Enqueue(data);
            if (!_batchTimer.IsEnabled)
                _batchTimer.Start();
        }

        private void FlushBatch()
        {
            _batchTimer.Stop();
            var items = new List<T>();
            while (_pendingItems.TryDequeue(out var item))
                items.Add(item);
            if (items.Count == 0) return;

            int totalAfterAdd = Model.DataCollection.Count + items.Count;
            if (totalAfterAdd > _retainNum)
            {
                int removeCount = totalAfterAdd - _retainNum;
                if (removeCount > Model.DataCollection.Count)
                    removeCount = Model.DataCollection.Count;
                for (int i = 0; i < removeCount; i++)
                    Model.DataCollection.RemoveAt(0);
            }
            Model.DataCollection.AddRange(items);
        }

        public virtual void Clear()
        {
            try
            {
                Action action = () => { Model.DataCollection.Clear(); };
                action.UIBeginInvoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DataGridService.Clear 异常: {ex.Message}");
            }
        }

        public virtual void ShowData(List<T> data)
        {
            try
            {
                Action action = () =>
                {
                    Model.DataCollection.Clear();
                    if (data != null)
                        Model.DataCollection.AddRange(data);
                };
                action.UIBeginInvoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DataGridService.ShowData 异常: {ex.Message}");
            }
        }
    }
}
