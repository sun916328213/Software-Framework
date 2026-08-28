using MaterialDesignThemes.Wpf;
using System;
using System.Collections.ObjectModel;

namespace WPFControl.ProgramControl.Models
{
    public class CControlItem
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
        /// 默认图标
        /// </summary>
        public PackIconKind Icon { get; set; }
        /// <summary>
        /// 函数
        /// </summary>
        public Action Action;
    }

    /// <summary>
    /// 控制菜单数据模型
    /// 负责存储控制菜单的项目集合
    /// </summary>
    public class ControlModel
    {
        /// <summary>
        /// 控制菜单项目集合
        /// </summary>
        internal ObservableCollection<CControlItem> ControlItems { get; set; } = new ObservableCollection<CControlItem>();
 

        /// <summary>
        /// 构造函数
        /// 初始化默认的控制菜单项
        /// </summary>
        public ControlModel()
        {
            ControlItems.Add(new CControlItem() { Title = "初始化", SelectedIcon = PackIconKind.Replay, Icon = PackIconKind.Restore });
            ControlItems.Add(new CControlItem() { Title = "启动", SelectedIcon = PackIconKind.Play, Icon = PackIconKind.PlayOutline });
            ControlItems.Add(new CControlItem() { Title = "停止", SelectedIcon = PackIconKind.StopCircle, Icon = PackIconKind.StopCircleOutline });
            ControlItems.Add(new CControlItem() { Title = "退出", SelectedIcon = PackIconKind.Power, Icon = PackIconKind.PowerStandby });
        }
    }
}