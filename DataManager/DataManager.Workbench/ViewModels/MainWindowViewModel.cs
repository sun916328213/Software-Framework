using DataManager.Workbench.Infrastructure;
using Prism.Mvvm;
using Prism.Regions;
using System.Collections.ObjectModel;

namespace DataManager.Workbench.ViewModels
{
    /// <summary>
    /// 导航项
    /// </summary>
    public class NavItem
    {
        public string Title { get; }
        public string Badge { get; }
        public string ViewName { get; }

        public NavItem(string title, string badge, string viewName)
        {
            Title = title;
            Badge = badge;
            ViewName = viewName;
        }
    }

    /// <summary>
    /// 主窗口视图模型：侧栏导航 + 区域跳转
    /// </summary>
    public class MainWindowViewModel : BindableBase
    {
        private readonly IRegionManager _regionManager;
        private NavItem? _selectedNav;

        public ObservableCollection<NavItem> NavItems { get; }

        public NavItem? SelectedNav
        {
            get => _selectedNav;
            set
            {
                if (SetProperty(ref _selectedNav, value) && value != null)
                    NavigateTo(value.ViewName);
            }
        }

        public MainWindowViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;

            NavItems = new ObservableCollection<NavItem>
            {
                new("SQL 数据库测试", "SQL", "SqlView"),
                new("日志测试", "LOG", "LogView"),
                new("Excel 测试", "XLS", "ExcelView"),
            };
        }

        /// <summary>窗口加载后定位到默认页面</summary>
        public void SelectFirst() => NavigateTo("SqlView");

        private void NavigateTo(string viewName)
        {
            if (_regionManager.Regions.ContainsRegionWithName(RegionNames.MainRegion))
                _regionManager.RequestNavigate(RegionNames.MainRegion, viewName);
        }
    }
}
