// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：MainWindow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Workbench、DataManager.Workbench.ViewModels、System.Windows。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Workbench;
using DataManager.Workbench.ViewModels;
using System.Windows;

namespace DataManager.Workbench.Views
{
    /// <summary>
    /// MainWindow 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
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
