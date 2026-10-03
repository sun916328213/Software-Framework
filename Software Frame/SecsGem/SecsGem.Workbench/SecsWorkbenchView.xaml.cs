// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：SecsWorkbenchView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Controls.Services、SecsGem.Controls.Terminal、SecsGem.Controls.Views、SecsGem.Runtime、SecsGem.Workbench.ViewModels、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using SecsGem.Controls.Services;
using SecsGem.Controls.Terminal;
using SecsGem.Controls.Views;
using SecsGem.Runtime;
using SecsGem.Workbench.ViewModels;
using System.Windows.Controls;

namespace SecsGem.Workbench;

/// <summary>兼容工作台主页；完整页面由 Controls 提供。</summary>
public sealed class SecsWorkbenchView : UserControl, IDisposable
{
    private readonly SecsView _view;
    /// <summary>
    /// 初始化 SecsWorkbenchView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 Create、EnableWorkbenchCommandHandler；具体条件和先后次序见方法体。
    /// </summary>
    public SecsWorkbenchView(SecsWorkspace workspace, SecsGemRuntime runtime)
    {
        var viewModel = new SecsWorkbenchViewModel(workspace);
        DataContext = viewModel;
        Content = _view = SecsView.Create(viewModel);
        runtime.TerminalDisplay = new WpfTerminalDisplay();
        workspace.EnableWorkbenchCommandHandler();
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 FindName。
    /// 正文中的关键协作调用包括 FindName；具体条件和先后次序见方法体。
    /// </summary>
    public new object FindName(string name) => _view.FindName(name);
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose() => _view.Dispose();
}
