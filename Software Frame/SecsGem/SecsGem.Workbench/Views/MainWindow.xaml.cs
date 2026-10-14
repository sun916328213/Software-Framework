// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：MainWindow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、SecsGem.Workbench.ViewModels。导入不代表所有类型都会在运行时加载。
using System.Windows;

namespace SecsGem.Workbench.Views
{
    /// <summary>Hosts the shell view model; navigation begins after module initialization.</summary>
    /// <remarks>
    /// MainWindow 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
    /// 继承或实现的契约：Window。具体约束按这些契约及各成员实现解释。
    /// </remarks>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 初始化 MainWindow 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            // DataContext 交给 Prism ViewModelLocator 按 Views.MainWindow → ViewModels.MainWindowViewModel 约定注入，
            // 与 Device.Workbench 的 MainWindow 同一口径；命名空间必须落在 .Views 下，否则约定不命中。
        }
    }
}
