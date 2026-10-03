// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：MainWindow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench、Device.Workbench.ViewModels、System.Windows。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
using Device.Workbench.ViewModels;
using System.Windows;

namespace Device.Workbench.Views
{
    /// <summary>
    /// MainWindow 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
    /// 继承或实现的契约：Window。具体约束按这些契约及各成员实现解释。
    /// </summary>
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
}
