// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：MainWindow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、FlowNodes.Workbench.ViewModels。导入不代表所有类型都会在运行时加载。
using System.Windows;
using FlowNodes.Workbench.ViewModels;

namespace FlowNodes.Workbench.Views;

/// <summary>Hosts the shell view model; navigation begins after module initialization.</summary>
/// <remarks>
/// MainWindow 的类型定义。FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
/// 继承或实现的契约：Window。具体约束按这些契约及各成员实现解释。
/// </remarks>
public partial class MainWindow : Window
{
    /// <summary>
    /// 初始化 MainWindow 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
    /// </summary>
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
