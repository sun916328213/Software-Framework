using Extension.Core.ControlEx;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.ViewModels;

namespace WPFControl.ProgramControl.Views
{
    /// <summary>
    /// MenuView.xaml 的交互逻辑
    /// </summary>
    public partial class MenuView : UserControl
    {
        #region 构造函数

        public MenuView()
        {
            InitializeComponent();
            this.Loaded += MenuView_Loaded;
            MenuTreeView.PreviewMouseWheel += MenuTreeView_PreviewMouseWheel;
            MenuTreeView_Left.PreviewMouseWheel += MenuTreeView_PreviewMouseWheel;
        }

        /// <summary>
        /// 滚轮事件：将滚轮操作转发给外层 ScrollViewer，
        /// 避免二级菜单 ListBox（禁用了自身滚动）吞掉滚轮事件导致无法滚动
        /// </summary>
        private void MenuTreeView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 控件加载完成事件
        /// </summary>
        private void MenuView_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始化抽屉状态
            Switch(true);

            // 默认执行第一个菜单项
            if (this.DataContext is MenuViewModel viewModel && 
                viewModel.Datas.NavigationItems.Count > 0)
            {
                var firstItem = viewModel.Datas.NavigationItems[0];
                viewModel.SelectedCommand.Execute(firstItem);
            }
        }

        #endregion

        #region 菜单点击事件

        /// <summary>
        /// 一级菜单按钮点击事件
        /// </summary>
        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && GetNavigationItem(button) is CNavigationItem item)
            {
                // 执行导航命令（设置选中状态）
                if (this.DataContext is MenuViewModel viewModel)
                {
                    viewModel.SelectedCommand.Execute(item);
                }

                // 如果是一级菜单且有子菜单，切换展开/收起状态
                if (item.IsParent)
                {
                    ToggleExpander(button);
                }
            }
        }

        /// <summary>
        /// 二级菜单按钮点击事件
        /// </summary>
        private void SubMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CNavigationItem item)
            {
                // 设置 ListBoxItem 为选中状态
                SetListBoxItemSelected(button);

                // 执行导航命令
                if (this.DataContext is MenuViewModel viewModel)
                {
                    viewModel.SelectedCommand.Execute(item);
                }
            }
        }

        #endregion

        #region 抽屉切换

        /// <summary>
        /// 抽屉切换按钮点击事件
        /// </summary>
        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool isOpen = DrawerHost.IsLeftDrawerOpen;
            Switch(!isOpen);
        }

        /// <summary>
        /// 切换菜单显示模式（展开/收起抽屉）
        /// </summary>
        /// <param name="isOpen">是否打开抽屉</param>
        public void Switch(bool isOpen)
        {
            int scrollbarOffset = 0;

            if (isOpen)
            {
                // 打开抽屉模式
                bool isScrollBarVisible = ScrollBarEx.ScrollBarVisible(MenuTreeView_Left, Orientation.Vertical);
                scrollbarOffset = isScrollBarVisible ? 0 : 2;

                DrawerHost.IsLeftDrawerOpen = true;
                MenuTreeView.Visibility = Visibility.Collapsed;
                Border.Width = MenuTreeView_Left.Width + 8 - scrollbarOffset;
            }
            else
            {
                // 关闭抽屉模式（显示主菜单）
                bool isScrollBarVisible = ScrollBarEx.ScrollBarVisible(MenuTreeView, Orientation.Vertical);
                scrollbarOffset = isScrollBarVisible ? 0 : 2;

                DrawerHost.IsLeftDrawerOpen = false;
                MenuTreeView.Visibility = Visibility.Visible;
                Border.Width = MenuTreeView.Width + 8 - scrollbarOffset;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 从按钮获取导航菜单项
        /// </summary>
        private CNavigationItem? GetNavigationItem(Button button)
        {
            // 优先从 DataContext 获取
            CNavigationItem? item = button.DataContext as CNavigationItem;
            
            // 如果 DataContext 不是 CNavigationItem，尝试从 Tag 获取
            if (item == null)
            {
                item = button.Tag as CNavigationItem;
            }

            return item;
        }

        /// <summary>
        /// 切换 Expander 的展开/收起状态
        /// </summary>
        private void ToggleExpander(Button button)
        {
            if (FindVisualParent<Expander>(button) is Expander expander)
            {
                expander.IsExpanded = !expander.IsExpanded;
            }
        }

        /// <summary>
        /// 设置 ListBoxItem 为选中状态
        /// </summary>
        private void SetListBoxItemSelected(Button button)
        {
            if (FindVisualParent<ListBoxItem>(button) is ListBoxItem listBoxItem)
            {
                listBoxItem.IsSelected = true;
            }
        }

        /// <summary>
        /// 查找可视化树中的父级控件
        /// </summary>
        /// <typeparam name="T">父级控件类型</typeparam>
        /// <param name="child">子控件</param>
        /// <returns>找到的父级控件，未找到返回 null</returns>
        private T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            
            while (parent != null)
            {
                if (parent is T typedParent)
                {
                    return typedParent;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            
            return null;
        }

        #endregion

        #region 搜索功能

        /// <summary>
        /// 搜索框文本变化事件
        /// </summary>
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.DataContext is MenuViewModel viewModel && sender is TextBox textBox)
            {
                viewModel.SearchText = textBox.Text;
                viewModel.FilterMenuItems();
            }
        }

        #endregion
    }
}