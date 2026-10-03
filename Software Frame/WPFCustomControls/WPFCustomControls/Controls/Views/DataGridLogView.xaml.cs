// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：DataGridLogView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Collections、System.Collections.Specialized、System.Windows、System.Windows.Controls、System.Windows.Threading。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
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

        /// <summary>
        /// 初始化 DataGridLogView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public DataGridLogView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnItemsSourceChanged。
        /// 正文中的关键协作调用包括 OnItemsSourceChanged；具体条件和先后次序见方法体。
        /// </summary>
        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);

            if (oldValue is INotifyCollectionChanged oldColl)
                oldColl.CollectionChanged -= OnItemsSourceCollectionChanged;

            if (newValue is INotifyCollectionChanged newColl)
                newColl.CollectionChanged += OnItemsSourceCollectionChanged;
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnItemsSourceCollectionChanged。
        /// 正文中的关键协作调用包括 BeginInvoke、ScrollIntoView；具体条件和先后次序见方法体。
        /// </summary>
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
