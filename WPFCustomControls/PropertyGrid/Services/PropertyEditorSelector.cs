using System.Windows;
using System.Windows.Controls;
using WPFCustomControls.PropertyGrid.ViewModels;

namespace WPFCustomControls.PropertyGrid.Services;

/// <summary>
/// 根据 PropertyItemViewModel.PropertyType 动态选择对应的编辑器 DataTemplate。
/// 支持文本/数值（TextBox）、布尔（CheckBox）、枚举（ComboBox）、集合（ItemsControl）、
/// 数值微调（带增减按钮）等内置编辑器。
/// </summary>
public class PropertyEditorSelector : DataTemplateSelector
{
    /// <summary>文本/数值类型编辑器模板（TextBox）。</summary>
    public DataTemplate? TextTemplate { get; set; }

    /// <summary>布尔类型编辑器模板（CheckBox，可空时三态）。</summary>
    public DataTemplate? BoolTemplate { get; set; }

    /// <summary>枚举类型编辑器模板（ComboBox，支持显示名）。</summary>
    public DataTemplate? EnumTemplate { get; set; }

    /// <summary>集合类型编辑器模板（ItemsControl）。</summary>
    public DataTemplate? CollectionTemplate { get; set; }

    /// <summary>数值类型编辑器模板（TextBox + 增减按钮）。</summary>
    public DataTemplate? NumericTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is PropertyItemViewModel vm)
        {
            // 集合类型用集合模板
            if (vm.IsCollection)
                return CollectionTemplate;

            // 枚举（含可空枚举）用 ComboBox
            if (vm.EnumOptions != null)
                return EnumTemplate;

            // 布尔（含可空布尔）用 CheckBox
            if (vm.IsBoolean)
                return BoolTemplate;

            // 数值（含可空数值）用带增减按钮的编辑器
            if (vm.IsNumeric)
                return NumericTemplate;

            // 默认文本编辑
            return TextTemplate;
        }
        return base.SelectTemplate(item, container);
    }
}
