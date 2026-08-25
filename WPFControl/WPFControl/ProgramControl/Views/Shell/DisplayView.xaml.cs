using HalconDotNet;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WPFControl.CustomControl.Views.Display;
using WPFControl.ProgramControl.ViewModels;

namespace WPFControl.ProgramControl.Views
{
    /// <summary>
    /// DisplayView.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayView : UserControl
    {
        private DisplayViewModel _viewModel;

        public DisplayView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

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

            // 绑定 HTool
            var binding = new Binding($"VisionTools[{index}]")
            {
                Source = _viewModel,
                Mode = BindingMode.TwoWay
            };
            display.SetBinding(DisplaySingleView_DP.HToolProperty, binding);

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