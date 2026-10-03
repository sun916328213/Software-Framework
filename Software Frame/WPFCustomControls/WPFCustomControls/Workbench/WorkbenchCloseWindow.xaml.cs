// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：WorkbenchCloseWindow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.ComponentModel。导入不代表所有类型都会在运行时加载。
using System.ComponentModel;

namespace WPFCustomControls.Workbench;

/// <summary>Displays close progress without blocking the owner dispatcher.</summary>
/// <remarks>
/// WorkbenchCloseWindow 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// 继承或实现的契约：System.Windows.Window。具体约束按这些契约及各成员实现解释。
/// </remarks>
public partial class WorkbenchCloseWindow : System.Windows.Window
{
    private bool _allowClose;

    /// <summary>
    /// 初始化 WorkbenchCloseWindow 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
    /// </summary>
    public WorkbenchCloseWindow(System.Windows.Window owner, WorkbenchCloseViewModel viewModel)
    {
        InitializeComponent();
        Owner = owner;
        DataContext = viewModel;
        Closing += PreventPrematureClose;
        SizeChanged += (_, _) =>
        {
            // Timeout text and actions increase SizeToContent after the initial placement.
            if (!IsVisible) return;
            Left = Owner.Left + (Owner.ActualWidth - ActualWidth) / 2;
            Top = Owner.Top + (Owner.ActualHeight - ActualHeight) / 2;
        };
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Finish。
    /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
    /// </summary>
    internal void Finish()
    {
        _allowClose = true;
        Close();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PreventPrematureClose。
    /// </summary>
    private void PreventPrematureClose(object? sender, CancelEventArgs args) => args.Cancel = !_allowClose;
}
