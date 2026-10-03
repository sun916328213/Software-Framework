// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：NameInputControl。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Windows、System.Windows.Input。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Windows;
using System.Windows.Input;

namespace WPFCustomControls.Window.Views;

/// <summary>
/// 名称输入弹窗（Window 形式，无边框）。
/// 直接通过属性设置标题/描述/占位符，无需 ViewModel 中间层。
/// </summary>
public partial class NameInputControl : System.Windows.Window
{
    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(NameInputControl), new PropertyMetadata("请输入名称："));
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(NameInputControl), new PropertyMetadata("名称"));
    public static readonly DependencyProperty InputTextProperty =
        DependencyProperty.Register(nameof(InputText), typeof(string), typeof(NameInputControl), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string InputText { get => (string)GetValue(InputTextProperty); set => SetValue(InputTextProperty, value); }

    /// <summary>弹出名称输入框，返回输入文本或 null（取消）。</summary>
    public static string? Prompt(string title, string description, string placeholder = "名称")
    {
        var dlg = new NameInputControl
        {
            Title = title,
            Description = description,
            Placeholder = placeholder
        };
        return dlg.ShowDialog() == true ? dlg.InputText : null;
    }

    /// <summary>
    /// 初始化 NameInputControl 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent、Focus；具体条件和先后次序见方法体。
    /// </summary>
    public NameInputControl()
    {
        InitializeComponent();
        Title = "输入名称";
        DataContext = this;
        Loaded += (_, _) => InputBox.Focus();
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnConfirmClick。
    /// </summary>
    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnCancelClick。
    /// </summary>
    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 InputBox_OnKeyDown。
    /// </summary>
    private void InputBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            DialogResult = true;
    }
}
