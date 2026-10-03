// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：MenuViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、System.Collections.ObjectModel、System.Linq、WPFControls.ProgramControl.Models。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels
{
    /// <summary>
    /// MenuViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

        /// <summary>
        /// 初始化 MenuViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 SwitchMenu；具体条件和先后次序见方法体。
        /// </summary>
        public MenuViewModel(MenuService model)
        {
            Service = model;

            SelectedCommand = new RelayCommand<CNavigationItem>((item) =>
            {
                if (item != null)
                    Service.SwitchMenu(item);
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