// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：Home。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Prism.Regions、Prism.Ioc、System.Windows、System.Windows.Controls、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using Prism.Regions;
using Prism.Ioc;
using System.Windows;
using System.Windows.Controls;
using WPFControls.ProgramControl.Services;

namespace MachineApp.UI.View
{
    /// <summary>
    /// Home.xaml 的交互逻辑
    /// </summary>
    public partial class Home : UserControl
    {
        private readonly IRegionManager _regionManager;
        private readonly IContainerProvider _container;

        LogInfoService _LogInfoService => _container.Resolve<LogInfoService>();

        /// <summary>
        /// 初始化 Home 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent、Resolve；具体条件和先后次序见方法体。
        /// </summary>
        public Home(IContainerProvider container )
        {
            InitializeComponent();
            _regionManager = container.Resolve<IRegionManager>();
            _container = container;
            // 在视图完全加载后再执行导航
            this.Loaded += Home_Loaded;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Home_Loaded。
        /// 正文中的关键协作调用包括 RequestNavigate；具体条件和先后次序见方法体。
        /// </summary>
        private void Home_Loaded(object sender, RoutedEventArgs e)
        {

            //_regionManager.RequestNavigate("DisplayRegion", "DisplayView");

            //_regionManager.RequestNavigate("DisplayRegion3", "StatisticsView");

            _regionManager.RequestNavigate("DisplayRegion1", "BulletinBoardView");

            _regionManager.RequestNavigate("DisplayRegion2", "LogInfoView");

            _regionManager.RequestNavigate("DisplayRegion3", "LogAlarmInfoView");

            _regionManager.RequestNavigate("DisplayRegion4", "SEMILogView");

        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Button_Click。
        /// 正文中的关键协作调用包括 AddRunInfo；具体条件和先后次序见方法体。
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _LogInfoService.AddRunInfo("主界面测试");
        }
    }
}