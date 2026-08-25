using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using WPFControl.CustomControl.Models;

namespace WPFControl.ProgramControl.Models
{
    /// <summary>
    /// 菜单项类型枚举
    /// </summary>
    public enum NavigationItemType
    {
        Normal,
        UserLogin,
        // 可以根据需要添加更多类型
    }

    /// <summary>
    /// 导航菜单项
    /// </summary>
    public class CNavigationItem : ObservableObject, IEquatable<CNavigationItem>
    {
        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// 选中图标
        /// </summary>
        public PackIconKind SelectedIcon { get; set; }
        /// <summary>
        /// 未选中图标
        /// </summary>
        public PackIconKind UnselectedIcon { get; set; }
        /// <summary>
        /// 视图
        /// </summary>
        public UIElement View { get; set; }
        /// <summary>
        /// 动作
        /// </summary>
        public Action Action { get; set; }
        /// <summary>
        /// 用户等级权限要求
        /// </summary>
        public EUserLevel Userlevel { get; set; }

        /// <summary>
        /// 菜单项类型
        /// </summary>
        public NavigationItemType ItemType { get; set; } = NavigationItemType.Normal;

        /// <summary>
        /// 是否可见（根据用户权限动态计算）
        /// </summary>
        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        /// <summary>
        /// 是否启用(根据用户权限动态计算)
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 子菜单集合
        /// </summary>
        public ObservableCollection<CNavigationItem> Children { get; set; } = new ObservableCollection<CNavigationItem>();

        /// <summary>
        /// 是否为父级菜单(有子菜单)
        /// </summary>
        public bool IsParent => Children.Count > 0;

        private bool isExpanded = false;
        /// <summary>
        /// 菜单是否展开(仅对父级菜单有效)
        /// </summary>
        public bool IsExpanded 
        { 
            get => isExpanded;
            set => SetProperty(ref isExpanded, value);
        }

        /// <summary>
        /// 检查当前用户是否有权限访问此菜单项
        /// </summary>
        /// <param name="currentUserLevel">当前用户权限级别</param>
        /// <returns>是否有权限</returns>
        public bool HasPermission(EUserLevel currentUserLevel)
        {
            return currentUserLevel >= Userlevel;
        }

        /// <summary>
        /// 更新菜单项的可见性和可用性
        /// </summary>
        /// <param name="currentUserLevel">当前用户权限级别</param>
        public void UpdatePermissionState(EUserLevel currentUserLevel)
        {
            bool hasPermission = HasPermission(currentUserLevel);
            IsVisible = hasPermission;
            IsEnabled = hasPermission;
        }

        /// <summary>
        /// 比较两个菜单项是否相等
        /// 使用引用比较，确保只有同一个对象实例才相等
        /// </summary>
        /// <param name="other">要比较的菜单项</param>
        /// <returns>是否相等</returns>
        public bool Equals(CNavigationItem other)
        {
            // 使用引用比较
            return ReferenceEquals(this, other);
        }

        /// <summary>
        /// 重写 GetHashCode 方法
        /// </summary>
        /// <returns>哈希码</returns>
        public override int GetHashCode()
        {
            // 使用默认的对象哈希码
            return base.GetHashCode();
        }

        /// <summary>
        /// 重写 Equals 方法
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>是否相等</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as CNavigationItem);
        }
    }

    public class MenuModel : ObservableObject
    {
        // 所有菜单项（包括被权限隐藏的）
        internal List<CNavigationItem> AllNavigationItems = new List<CNavigationItem>();


        ObservableCollection<CNavigationItem> cNavigationItems=new ObservableCollection<CNavigationItem>();
        /// <summary>
        /// 导航菜单集合
        /// </summary>
        public ObservableCollection<CNavigationItem> NavigationItems
        {
            get => cNavigationItems;
            set=>SetProperty(ref cNavigationItems, value);
        }

 
        EUserLevel iCurrentUserLevel = EUserLevel.Guest;

        // 当前用户权限级别
        public EUserLevel CurrentUserLevel
        {
            get => iCurrentUserLevel;
            set => SetProperty(ref iCurrentUserLevel, value);
        }

        /// <summary>
        /// 上一次选择菜单
        /// </summary>
        public CNavigationItem LastNavigationItems { get; set; }

        CNavigationItem cNavigationItem = new CNavigationItem();
        /// <summary>
        /// 当前选择菜单
        /// </summary>
        public CNavigationItem SelectNavigationItems
        {
            get => cNavigationItem;
            set => SetProperty(ref cNavigationItem, value);
        }
    }
}