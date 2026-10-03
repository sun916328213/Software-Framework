// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：CControlItem、ControlModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、MaterialDesignThemes.Wpf、System、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.ObjectModel;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// CControlItem 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
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