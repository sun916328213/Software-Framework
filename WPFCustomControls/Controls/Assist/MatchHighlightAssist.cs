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

    public static class MatchHighlightAssist
    {
        // 使用属性继承将一个状态对象传入 DataGrid/ListView 的单元格模板，避免每个页面重复绑定高亮参数。
        public static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
            "State",
            typeof(IMatchHighlightState),
            typeof(MatchHighlightAssist),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

        public static IMatchHighlightState? GetState(DependencyObject element)
        {
            return (IMatchHighlightState?)element.GetValue(StateProperty);
        }

        public static void SetState(DependencyObject element, IMatchHighlightState? value)
        {
            element.SetValue(StateProperty, value);
        }
    }
}
