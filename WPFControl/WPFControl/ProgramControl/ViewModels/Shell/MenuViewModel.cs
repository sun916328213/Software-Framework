using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    public class MenuViewModel : ObservableObject
    {
        private readonly MenuService Service;
        private string _searchText;

        public MenuModel Datas => Service.Model;

        /// <summary>
        /// 搜索文本
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }
 
        // 添加命令来处理菜单项选择
        public IRelayCommand<CNavigationItem> SelectedCommand { get; }

        public MenuViewModel(MenuService model)
        {
            Service = model;

            SelectedCommand = new RelayCommand<CNavigationItem>((item) =>
            {
                if (item != null)
                {
                    // 设置当前选中的菜单项（一级或二级都显示灰色）
                    Service.Model.SelectNavigationItems = item;
                    Service.ExecuteNavigationItem();
                }
            });
        }

        /// <summary>
        /// 过滤菜单项
        /// </summary>
        public void FilterMenuItems()
        {
            System.Diagnostics.Debug.WriteLine($"搜索: '{SearchText}'");
            
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // 搜索框为空，恢复权限控制的可见性（而非强制全部显示）
                foreach (var item in Datas.NavigationItems)
                {
                    item.UpdatePermissionState(Service.GetCurrentUserLevel());
                    if (item.Children != null)
                    {
                        foreach (var child in item.Children)
                        {
                            child.UpdatePermissionState(Service.GetCurrentUserLevel());
                        }
                    }
                }
            }
            else
            {
                var currentLevel = Service.GetCurrentUserLevel();

                // 根据搜索文本过滤（同时检查权限）
                foreach (var item in Datas.NavigationItems)
                {
                    bool itemMatch = item.Title?.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) == true;
                    
                    if (item.Children != null)
                    {
                        foreach (var child in item.Children)
                        {
                            bool childMatch = child.Title?.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) == true;
                            // 搜索显示和权限检查同时生效：匹配搜索 且 有权限
                            child.IsVisible = (itemMatch || childMatch) && child.HasPermission(currentLevel);
                        }
                        item.IsVisible = itemMatch || item.Children.Any(c => c.IsVisible);
                    }
                    else
                    {
                        item.IsVisible = itemMatch;
                    }
                }
            }
        }
    }
}