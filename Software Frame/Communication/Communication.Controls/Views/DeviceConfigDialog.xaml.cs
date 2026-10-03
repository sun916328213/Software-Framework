// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：DeviceConfigDialog、ParamFieldTemplateSelector。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Controls、System.Windows、System.Windows.Controls、Communication.Controls.ViewModels。导入不代表所有类型都会在运行时加载。
using Communication.Controls;
using System.Windows;
using System.Windows.Controls;
using Communication.Controls.ViewModels;

namespace Communication.Controls.Views;

/// <summary>设备配置对话框视图（由 Prism DialogWindow 宿主承载）—— 按参数类型选择编辑模板。</summary>
public partial class DeviceConfigDialog : UserControl
{
    /// <summary>
    /// 初始化 DeviceConfigDialog 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
    /// </summary>
    public DeviceConfigDialog(DeviceConfigDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

/// <summary>参数字段模板选择器：可编辑下拉（串口号）→ 可输入 ComboBox，布尔 → CheckBox，枚举 → ComboBox，其余 → 文本框。</summary>
public sealed class ParamFieldTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }

    public DataTemplate? BoolTemplate { get; set; }

    public DataTemplate? OptionTemplate { get; set; }

    public DataTemplate? EditableOptionTemplate { get; set; }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 SelectTemplate。
    /// </summary>
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not ParamField field)
        {
            return TextTemplate;
        }

        if (field.IsEditableCombo)
        {
            return EditableOptionTemplate ?? OptionTemplate;
        }

        if (field.IsBoolean)
        {
            return BoolTemplate;
        }

        return field.IsOption ? OptionTemplate : TextTemplate;
    }
}
