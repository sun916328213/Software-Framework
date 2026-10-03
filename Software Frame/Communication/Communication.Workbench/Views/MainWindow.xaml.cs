// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：MainWindow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Workbench、Communication.Workbench.ViewModels、System.Windows。导入不代表所有类型都会在运行时加载。
using Communication.Workbench;
using Communication.Workbench.ViewModels;
using System.Windows;

namespace Communication.Workbench.Views;

/// <summary>壳窗口：侧栏导航 + Prism 区域，DataContext 由 Prism 约定自动装配。</summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 初始化 MainWindow 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}
