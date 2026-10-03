// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：IMatchHighlightState、MatchHighlightAssist。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.ComponentModel、System.Windows。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.ComponentModel;
using System.Windows;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// 描述结果文本高亮所需的只读状态，避免底层控件依赖具体查询上下文。
    /// </summary>
    public interface IMatchHighlightState : INotifyPropertyChanged
    {
        string FieldKey { get; }

        string SearchText { get; }

        bool WholeWord { get; }

        bool CaseSensitive { get; }

        int ActiveRowIndex { get; }

        int ActiveMatchStart { get; }
    }

    /// <summary>
    /// MatchHighlightAssist 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
    /// </summary>
    public static class MatchHighlightAssist
    {
        // 使用属性继承将一个状态对象传入 DataGrid/ListView 的单元格模板，避免每个页面重复绑定高亮参数。
        public static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
            "State",
            typeof(IMatchHighlightState),
            typeof(MatchHighlightAssist),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetState。
        /// 正文中的关键协作调用包括 GetValue；具体条件和先后次序见方法体。
        /// </summary>
        public static IMatchHighlightState? GetState(DependencyObject element)
        {
            return (IMatchHighlightState?)element.GetValue(StateProperty);
        }

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetState。
        /// 正文中的关键协作调用包括 SetValue；具体条件和先后次序见方法体。
        /// </summary>
        public static void SetState(DependencyObject element, IMatchHighlightState? value)
        {
            element.SetValue(StateProperty, value);
        }
    }
}
