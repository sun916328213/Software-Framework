// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：DisplayView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、HalconDotNet、System.Windows、System.Windows.Controls、System.Windows.Data、System.Windows.Media。导入不代表所有类型都会在运行时加载。
using WPFControls;
using HalconDotNet;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WPFControls.ProgramControl.Views.Display;
using WPFControls.ProgramControl.ViewModels;

namespace WPFControls.ProgramControl.Views
{
    /// <summary>
    /// DisplayView.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayView : UserControl
    {
        private DisplayViewModel _viewModel;

        /// <summary>
        /// 初始化 DisplayView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public DisplayView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnDataContextChanged。
        /// </summary>
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _viewModel = e.NewValue as DisplayViewModel;
        }

        /// <summary>
        /// 当 ItemsControl 的每个容器 Loaded 时，绑定 HTool 并设置 Padding。
        /// </summary>
        private void OnItemLoaded(object sender, RoutedEventArgs e)
        {
            var container = (ContentPresenter)sender;
            var display = FindVisualChild<DisplaySingleView_DP>(container);
            if (display == null) return;

            int index = MainGrid.ItemContainerGenerator.IndexFromContainer(container);
            if (index < 0 || _viewModel == null) return;

            // 绑定显示图像
            var binding = new Binding($"DisplayImages[{index}]")
            {
                Source = _viewModel,
                Mode = BindingMode.TwoWay
            };
            display.SetBinding(DisplaySingleView_DP.DisplayImageProperty, binding);

            // 设置 Padding
            int columnCount = _viewModel.ColumnCount > 0 ? _viewModel.ColumnCount : 1;
            int cap = 4;
            int colIndex = index % columnCount;
            display.Padding = colIndex == 0 || colIndex == columnCount - 1
                ? new Thickness(cap, 0, cap, cap)
                : new Thickness(cap - 1, 0, cap - 1, cap);
        }

        /// <summary>
        /// 获取指定索引的 DisplaySingleView_DP 实例。
        /// </summary>
        private DisplaySingleView_DP GetDisplay(int index)
        {
            var container = MainGrid.ItemContainerGenerator.ContainerFromIndex(index) as ContentPresenter;
            return container == null ? null : FindVisualChild<DisplaySingleView_DP>(container);
        }
 
        /// <summary>
        /// 在可视化树中查找指定类型的子元素。
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}