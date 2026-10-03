// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：LogInfoView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System.Windows、System.Windows.Controls、WPFControls.ProgramControl.ViewModels。导入不代表所有类型都会在运行时加载。
using WPFControls;
using System.Windows;
using System.Windows.Controls;
using WPFControls.ProgramControl.ViewModels;

namespace WPFControls.ProgramControl.Views
{
    /// <summary>
    /// RunInfoView.xaml 的交互逻辑
    /// </summary>
    public partial class LogInfoView : UserControl
    {
        /// <summary>
        /// 初始化 LogInfoView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public LogInfoView()
        {
            InitializeComponent();
            IsVisibleChanged += OnIsVisibleChanged;
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnIsVisibleChanged。
        /// 正文中的关键协作调用包括 ScrollIntoView；具体条件和先后次序见方法体。
        /// </summary>
        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue == false) return;
            if (DataContext is LogInfoViewModel viewModel && viewModel.ItemsSource != null && viewModel.ItemsSource.Count > 0)
            {
                var lastItem = viewModel.ItemsSource[viewModel.ItemsSource.Count - 1];
                View_DataGrid.ScrollIntoView(lastItem);
            }
        }
    }
}