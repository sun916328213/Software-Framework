// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：PaginationView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.Input、System、System.ComponentModel、System.Windows、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WPFControls.Controls.Views
{
    /// <summary>
    /// PaginationView.xaml 的交互逻辑
    /// </summary>
    public partial class PaginationView : UserControl
    {
        public static readonly DependencyProperty TotalCountProperty = DependencyProperty.Register
            ("TotalCount", typeof(int), typeof(PaginationView), new PropertyMetadata(0, OnTotalCountChanged));

        public static readonly DependencyProperty PageSizeProperty = DependencyProperty.Register
            ("PageSize", typeof(int), typeof(PaginationView), new PropertyMetadata(20, OnPageSizeChanged));

        public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register
            ("CurrentPage", typeof(int), typeof(PaginationView),
                new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentPageChanged));

        public static readonly DependencyProperty TotalPagesProperty = DependencyProperty.Register
            ("TotalPages", typeof(int), typeof(PaginationView), new PropertyMetadata(0));

        public static readonly DependencyProperty PageChangedCommandProperty = DependencyProperty.Register
            ("PageChangedCommand", typeof(ICommand), typeof(PaginationView), new PropertyMetadata(null));

        [Bindable(true)]
        [Category("自定义属性"), Description("总记录数"), DisplayName("总记录数")]
        public int TotalCount
        {
            get { return (int)GetValue(TotalCountProperty); }
            set { SetValue(TotalCountProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("每页条数"), DisplayName("每页条数")]
        public int PageSize
        {
            get { return (int)GetValue(PageSizeProperty); }
            set { SetValue(PageSizeProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("当前页"), DisplayName("当前页")]
        public int CurrentPage
        {
            get { return (int)GetValue(CurrentPageProperty); }
            set { SetValue(CurrentPageProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("总页数"), DisplayName("总页数")]
        public int TotalPages
        {
            get { return (int)GetValue(TotalPagesProperty); }
            set { SetValue(TotalPagesProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("翻页指令"), DisplayName("翻页指令")]
        public ICommand PageChangedCommand
        {
            get { return (ICommand)GetValue(PageChangedCommandProperty); }
            set { SetValue(PageChangedCommandProperty, value); }
        }

        /// <summary>
        /// 当前页变化事件
        /// </summary>
        public event EventHandler PageChanged;

        public RelayCommand FirstCommand { get; }
        public RelayCommand PrevCommand { get; }
        public RelayCommand NextCommand { get; }
        public RelayCommand LastCommand { get; }

        /// <summary>
        /// 初始化 PaginationView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 GoToPage、InitializeComponent、ToString；具体条件和先后次序见方法体。
        /// </summary>
        public PaginationView()
        {
            FirstCommand = new RelayCommand(() => GoToPage(1));
            PrevCommand = new RelayCommand(() => GoToPage(CurrentPage - 1));
            NextCommand = new RelayCommand(() => GoToPage(CurrentPage + 1));
            LastCommand = new RelayCommand(() => GoToPage(TotalPages));

            InitializeComponent();
            PageNumberBox.Text = CurrentPage.ToString();
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnTotalCountChanged。
        /// 正文中的关键协作调用包括 Refresh；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnTotalCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PaginationView)d).Refresh();
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnPageSizeChanged。
        /// 正文中的关键协作调用包括 Refresh；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnPageSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PaginationView)d).Refresh();
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnCurrentPageChanged。
        /// 正文中的关键协作调用包括 NotifyCommands、SyncPageNumberBox；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnCurrentPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PaginationView)d;
            control.NotifyCommands();
            control.PageChanged?.Invoke(control, EventArgs.Empty);
            control.PageChangedCommand?.Execute(control.CurrentPage);
            control.SyncPageNumberBox();
        }

        /// <summary>
        /// 页码输入框回车提交
        /// </summary>
        private void PageNumberBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitPageNumber();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 页码输入框失焦提交
        /// </summary>
        private void PageNumberBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitPageNumber();
        }

        private bool _isCommitting;

        /// <summary>
        /// 解析输入框页码并跳转，非法输入自动还原为当前页
        /// </summary>
        private void CommitPageNumber()
        {
            if (_isCommitting)
                return;

            _isCommitting = true;
            try
            {
                if (TotalPages <= 0)
                {
                    PageNumberBox.Text = CurrentPage.ToString();
                    return;
                }

                if (int.TryParse(PageNumberBox.Text, out int page))
                {
                    page = Math.Max(1, Math.Min(TotalPages, page));
                    CurrentPage = page;
                }

                PageNumberBox.Text = CurrentPage.ToString();
            }
            finally
            {
                _isCommitting = false;
            }
        }

        /// <summary>
        /// 当前页被外部修改时同步输入框显示（输入框正在编辑时不覆盖）
        /// </summary>
        private void SyncPageNumberBox()
        {
            if (PageNumberBox != null && !PageNumberBox.IsKeyboardFocusWithin)
            {
                PageNumberBox.Text = CurrentPage.ToString();
            }
        }

        /// <summary>
        /// 根据总数与每页条数重新计算总页数并修正当前页
        /// </summary>
        private void Refresh()
        {
            int size = PageSize < 1 ? 1 : PageSize;
            TotalPages = TotalCount <= 0 ? 0 : (TotalCount + size - 1) / size;

            if (CurrentPage < 1)
                SetValue(CurrentPageProperty, 1);
            else if (TotalPages > 0 && CurrentPage > TotalPages)
                SetValue(CurrentPageProperty, TotalPages);
            else
                NotifyCommands();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 NotifyCommands。
        /// </summary>
        private void NotifyCommands()
        {
            FirstCommand?.NotifyCanExecuteChanged();
            PrevCommand?.NotifyCanExecuteChanged();
            NextCommand?.NotifyCanExecuteChanged();
            LastCommand?.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 GoToPage。
        /// 正文中的关键协作调用包括 Max、Min；具体条件和先后次序见方法体。
        /// </summary>
        private void GoToPage(int page)
        {
            if (TotalPages <= 0)
                return;
            page = Math.Max(1, Math.Min(TotalPages, page));
            CurrentPage = page;
        }
    }
}