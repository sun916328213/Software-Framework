using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// DataGridLogView.xaml 的交互逻辑
    /// 内建防抖滚动：批量添加只会触发一次 ScrollIntoView
    /// </summary>
    public partial class DataGridLogView : DataGrid
    {
        private bool _scrollPending;

        public DataGridLogView()
        {
            InitializeComponent();
        }

        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);

            if (oldValue is INotifyCollectionChanged oldColl)
                oldColl.CollectionChanged -= OnItemsSourceCollectionChanged;

            if (newValue is INotifyCollectionChanged newColl)
                newColl.CollectionChanged += OnItemsSourceCollectionChanged;
        }

        private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_scrollPending) return;
            _scrollPending = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _scrollPending = false;
                if (!IsVisible) return;
                if (ItemsSource is IList list && list.Count > 0)
                    ScrollIntoView(list[list.Count - 1]);
            }), DispatcherPriority.ApplicationIdle);
        }
    }
}
