using Extension.Core.ControlEx;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WPFControl.CustomControl.Models;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    public class MenuService 
    {
        public MenuModel Model { get; set; }=new MenuModel();

        readonly UserInfoService _UserInfoService;

        public MenuService() { }

        public MenuService(IContainerProvider container)
        {
            _UserInfoService = container.Resolve<UserInfoService>();
            InitializePermissionSystem();
        }
  
        // 用户权限变化事件
        public event Action<EUserLevel> OnUserLevelChanged;
  

        /// <summary>
        /// 初始化菜单权限系统，监听用户权限变化
        /// </summary>
        private  void InitializePermissionSystem()
        {
            if (_UserInfoService == null) return;

            // 监听用户信息变化（登录 / 登出 / 权限变更），实时刷新菜单可见性
            _UserInfoService.UserInfoChanged += (sender, model) =>
            {
                if (model?.CurUserInfo != null)
                {
                    SetCurrentUserLevel(model.CurUserInfo.Level);
                    SwitchMenu(0);
                }
            };

            // 设置初始用户权限
            SetCurrentUserLevel(_UserInfoService.Model.CurUserInfo.Level);
        }

        /// <summary>
        /// 设置当前用户权限级别并更新菜单显示
        /// </summary>
        /// <param name="userLevel">用户权限级别</param>
        public void SetCurrentUserLevel(EUserLevel userLevel)
        {
            if (Model.CurrentUserLevel != userLevel)
            {
                Model.CurrentUserLevel = userLevel;
                Action action = () => UpdateMenuVisibility();
                action.UIBeginInvoke();
                OnUserLevelChanged?.Invoke(userLevel);
            }
        }

        /// <summary>
        /// 获取当前用户权限级别
        /// </summary>
        /// <returns>当前用户权限级别</returns>
        public EUserLevel GetCurrentUserLevel()
        {
            return Model.CurrentUserLevel;
        }
 
        /// <summary>
        /// 根据当前用户权限更新菜单可见性（优化版本：不完全清除重建，而是直接修改）
        /// </summary>
        private void UpdateMenuVisibility()
        { 
            try
            {
                // 获取当前应该可见的菜单项，按照在_allNavigationItems中的原始顺序排列
                var visibleItems = Model.AllNavigationItems
                    .Where(item => item.HasPermission(Model.CurrentUserLevel))
                    .ToList(); // 移除了OrderBy和ThenBy，保持原始添加顺序

                // 使用索引跟踪来避免完全重建
                int targetIndex = 0;

                // 更新现有项的位置和状态
                foreach (var item in visibleItems)
                {
                    item.UpdatePermissionState(Model.CurrentUserLevel);
                    UpdateChildrenVisibility(item, Model.CurrentUserLevel);

                    // 检查该项是否已在NavigationItems中
                    int currentIndex = Model.NavigationItems.IndexOf(item);

                    if (currentIndex == -1)
                    {
                        // 项不存在，需要添加
                        if (targetIndex < Model.NavigationItems.Count)
                        {
                            Model.NavigationItems.Insert(targetIndex, item);
                        }
                        else
                        {
                            Model.NavigationItems.Add(item);
                        }
                    }
                    else if (currentIndex != targetIndex)
                    {
                        // 项存在但位置不正确，需要移动
                        Model.NavigationItems.Move(currentIndex, targetIndex);
                    }
                    // 如果位置正确，不需要操作

                    targetIndex++;
                }

                bool isChanged = false;
                // 移除不再应该可见的项
                for (int i = Model.NavigationItems.Count - 1; i >= targetIndex; i--)
                {
                    //判断选中页是否是移除页
                    if (Model.SelectNavigationItems == Model.NavigationItems[i])
                        isChanged=true;

                        Model.NavigationItems.RemoveAt(i);
                }

                //// 菜单项切换
                if (isChanged)
                {
                    Model.SelectNavigationItems=Model.NavigationItems[0];
                    ExecuteNavigationItem();
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序运行
               throw new Exception($"更新菜单可见性时发生错误: {ex}");
            }
        }

        /// <summary>
        /// 切换菜单项
        /// </summary>
        /// <param name="index"></param>
        public void SwitchMenu(int index)
        {
            if (index >= Model.NavigationItems.Count)
            {
                MessageBox.Show("Menu switch index out of range");
                Model.SelectNavigationItems = Model.NavigationItems[0];
            }
            ExecuteNavigationItem();
        }

        /// <summary>
        /// 切换菜单项（按标题查找，支持多级子菜单递归匹配）。
        /// </summary>
        /// <param name="name">目标菜单项标题</param>
        public void SwitchMenu(string name)
        {
            var item = FindNavigationItem(Model.NavigationItems, name);
            if (item == null)
            {
                MessageBox.Show("Menu item not found");
                return;
            }

            // 命中后切换到该项并执行其操作（原实现漏设 Model.SelectNavigationItems，
            // 仅一级查找且匹配成功后实际仍执行旧选中项）
            Model.SelectNavigationItems = item;
            ExecuteNavigationItem();
        }

        /// <summary>
        /// 切换菜单项
        /// </summary>
        /// <param name="item"></param>
        public void SwitchMenu(CNavigationItem item)
        {
            Model.SelectNavigationItems = item;
            ExecuteNavigationItem();
        }

        /// <summary>在菜单集合（含子菜单 Children）中按标题递归查找项。</summary>
        private static CNavigationItem? FindNavigationItem(IEnumerable<CNavigationItem> items, string name)
        {
            foreach (var it in items)
            {
                if (it.Title == name) return it;
                if (it.Children != null)
                {
                    var child = FindNavigationItem(it.Children, name);
                    if (child != null) return child;
                }
            }
            return null;
        }

        /// <summary>
        /// 执行菜单项操作
        /// </summary>

        private void ExecuteNavigationItem( )
        {
            var item = Model.SelectNavigationItems;
            if (item != null)
            {
                if (item != Model.LastNavigationItems|| item .ItemType== NavigationItemType.UserLogin)
                {
                    // 检查权限后执行操作
                    if (item.HasPermission(Model.CurrentUserLevel))
                    {
                        if (item.Action != null)
                        {
                            // 导航操作回到主线程
                           _= Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                item.Action.Invoke();
                            });
                        }
                    }
                    else
                    {
                        // 权限不足提示
                        System.Windows.MessageBox.Show(
                            $"权限不足，无法访问「{item.Title}」\n" +
                            $"当前用户级别：{(Model.CurrentUserLevel)}\n" +
                            $"所需级别：{(item.Userlevel)}",
                            "访问被拒绝",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                    }
                }
            }
        }
 

        public void Add(CNavigationItem pItem)
        {
            Action action = () =>
            {
                // 添加到所有菜单项集合
                Model.AllNavigationItems.Add(pItem);
        
                // 如果当前用户有权限,则添加到可见集合
                if (pItem.HasPermission(Model.CurrentUserLevel))
                {
                    pItem.UpdatePermissionState(Model.CurrentUserLevel);
                    Model.NavigationItems.Add(pItem);
                }
            };
            action.UIBeginInvoke();
        }
        
        /// <summary>
        /// 添加子菜单项
        /// </summary>
        /// <param name="parentItem">父菜单项</param>
        /// <param name="childItem">子菜单项</param>
        public void AddChild(CNavigationItem parentItem, CNavigationItem childItem)
        {
            if (parentItem == null || childItem == null)
                return;
        
            Action action = () =>
            {
                parentItem.Children.Add(childItem);
                childItem.UpdatePermissionState(Model.CurrentUserLevel);
            };
            action.UIBeginInvoke();
        }
        
        /// <summary>
        /// 批量添加子菜单项
        /// </summary>
        /// <param name="parentItem">父菜单项</param>
        /// <param name="childItems">子菜单项列表</param>
        public void AddChildren(CNavigationItem parentItem, List<CNavigationItem> childItems)
        {
            if (parentItem == null || childItems == null)
                return;
        
            Action action = () =>
            {
                foreach (var childItem in childItems)
                {
                    parentItem.Children.Add(childItem);
                    childItem.UpdatePermissionState(Model.CurrentUserLevel);
                }
            };
            action.UIBeginInvoke();
        }

        public void AddRange(List<CNavigationItem> pItems)
        {
            if (pItems == null) return;

            Action action = () =>
            {
                foreach (var item in pItems)
                {
                    // 添加到所有菜单项集合
                    Model.AllNavigationItems.Add(item);

                    // 如果当前用户有权限，则添加到可见集合
                    if (item.HasPermission(Model.CurrentUserLevel))
                    {
                        item.UpdatePermissionState(Model.CurrentUserLevel);
                        UpdateChildrenVisibility(item, Model.CurrentUserLevel);
                        Model.NavigationItems.Add(item);
                    }
                }
            };
            action.UIBeginInvoke();
        }

        /// <summary>
        /// 递归设置子菜单的可见性（不删除子项，仅控制 IsVisible）
        /// </summary>
        private void UpdateChildrenVisibility(CNavigationItem parent, EUserLevel currentUserLevel)
        {
            if (parent.Children == null) return;

            foreach (var child in parent.Children)
            {
                child.UpdatePermissionState(currentUserLevel);
                UpdateChildrenVisibility(child, currentUserLevel);
            }
        }

        public void Clear()
        {
            Action action = () =>
            {
                Model.NavigationItems.Clear();
                Model.AllNavigationItems.Clear();
            };
            action.UIBeginInvoke();
        }

    }
}
