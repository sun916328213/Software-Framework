// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：MainWindow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、Vision.Workbench.ViewModels。导入不代表所有类型都会在运行时加载。
using System.Windows;
using Vision.Workbench.ViewModels;

namespace Vision.Workbench;

/// <summary>Hosts the shell view model; navigation begins after module initialization.</summary>
/// <remarks>
/// MainWindow 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
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
