// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：InputPortEditDialog。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Windows、WPFCustomControls.PropertyGrid.Core.ViewModels。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Windows;
using WPFCustomControls.PropertyGrid.Core.ViewModels;

namespace WPFCustomControls.PropertyGrid.Views;

/// <summary>
/// InputPortEditDialog 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// 继承或实现的契约：System.Windows.Window。具体约束按这些契约及各成员实现解释。
/// </summary>
public partial class InputPortEditDialog : System.Windows.Window
{
    /// <summary>
    /// 初始化 InputPortEditDialog 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
    /// </summary>
    public InputPortEditDialog(InputPortEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnCloseRequested。
    /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
    /// </summary>
    private void OnCloseRequested(bool? dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }
}
