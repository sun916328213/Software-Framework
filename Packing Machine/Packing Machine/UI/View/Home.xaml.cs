using Prism.Regions;
using Prism.Ioc;
using System.Windows;
using System.Windows.Controls;
using WPFControl.ProgramControl.Services;

namespace Packing_Machine.UI.View
{
    /// <summary>
    /// Home.xaml 的交互逻辑
    /// </summary>
    public partial class Home : UserControl
    {
        private readonly IRegionManager _regionManager;
        private readonly IContainerProvider _container;

        LogInfoService _LogInfoService => _container.Resolve<LogInfoService>();

        public Home(IContainerProvider container )
        {
            InitializeComponent();
            _regionManager = container.Resolve<IRegionManager>();
            _container = container;
            // 在视图完全加载后再执行导航
            this.Loaded += Home_Loaded;
        }

        private void Home_Loaded(object sender, RoutedEventArgs e)
        {

            //_regionManager.RequestNavigate("DisplayRegion", "DisplayView");

            //_regionManager.RequestNavigate("DisplayRegion3", "StatisticsView");

            _regionManager.RequestNavigate("DisplayRegion1", "BulletinBoardView");

            _regionManager.RequestNavigate("DisplayRegion2", "LogInfoView");

            _regionManager.RequestNavigate("DisplayRegion3", "LogAlarmInfoView");

            _regionManager.RequestNavigate("DisplayRegion4", "SEMILogView");

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _LogInfoService.AddRunInfo("主界面测试");
        }
    }
}