using System.Windows;
using System.Windows.Controls;
using CommunicationTestApp.ViewModels;

namespace CommunicationTestApp.Views;

/// <summary>设备配置对话框视图（由 Prism DialogWindow 宿主承载）—— 按参数类型选择编辑模板。</summary>
public partial class DeviceConfigDialog : UserControl
{
    public DeviceConfigDialog(DeviceConfigDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

/// <summary>参数字段模板选择器：布尔 → CheckBox，枚举 → ComboBox，其余 → 文本框。</summary>
public sealed class ParamFieldTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }

    public DataTemplate? BoolTemplate { get; set; }

    public DataTemplate? OptionTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not ParamField field)
        {
            return TextTemplate;
        }

        if (field.IsBoolean)
        {
            return BoolTemplate;
        }

        return field.IsOption ? OptionTemplate : TextTemplate;
    }
}
