// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：FileLoading。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Windows。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Windows;

namespace WPFCustomControls.Window.Views;

/// <summary>
/// FileLoading 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// 继承或实现的契约：System.Windows.Window。具体约束按这些契约及各成员实现解释。
/// </summary>
public partial class FileLoading : System.Windows.Window
{
    public static readonly DependencyProperty TextProperty;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// 初始化 FileLoading 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
    /// </summary>
    public FileLoading() => InitializeComponent();

    /// <summary>
    /// 初始化 FileLoading 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
    /// </summary>
    static FileLoading()
    {
        TextProperty = DependencyProperty.Register("Text", typeof(string),
            typeof(FileLoading), new FrameworkPropertyMetadata("Loading....."));
    }
}