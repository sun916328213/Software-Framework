using Prism.Ioc;
using Prism.Regions;
using System;
using System.Windows;
using System.Windows.Controls;

namespace WPFControl.ProgramControl.Views
{
    /// <summary>
    /// CamManagerView.xaml 的交互逻辑
    /// </summary>
    public partial class CamManagerView : UserControl
    {
        IRegionManager _regionManager;
        public CamManagerView(IRegionManager regionManager)
        {
            InitializeComponent();
            _regionManager=regionManager;
            this.Loaded += CamManagerView_Loaded;
        }

        private void CamManagerView_Loaded(object sender, RoutedEventArgs e)
        {
            _regionManager.RequestNavigate("LightControlRegion", "LightControlView");
        }
    }
}