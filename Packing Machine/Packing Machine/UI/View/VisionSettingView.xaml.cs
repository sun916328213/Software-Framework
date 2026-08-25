using Prism.Ioc;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Packing_Machine.UI.View
{
    /// <summary>
    /// VisionSettingView.xaml 的交互逻辑
    /// </summary>
    public partial class VisionSettingView : UserControl
    {
        IRegionManager _regionManager;

        public VisionSettingView(IContainerProvider container)
        {
            InitializeComponent();

            _regionManager= container.Resolve<IRegionManager>();

            Loaded += (sender, e) =>
            {
                        _regionManager.RequestNavigate("ContentRegion", "VisionInspectionItemView");
            };
        }
    }
}
