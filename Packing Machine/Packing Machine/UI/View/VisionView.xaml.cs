using Device.Camera;
using Device.Camera.Gige;
using Device.Scan;
using Vision.Core.Vision;
using DPLibrary_Halcon_F48;
using DPLibrary_Halcon_F48.Tools;
using DPLibrary_Halcon_F48.Tools.FindLine;
using HalconDotNet;
using Packing_Machine.UI.ViewModel;
using Prism.Ioc;
using Prism.Regions;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPFControl.ProgramControl.Services;

namespace Packing_Machine.UI.View
{
    /// <summary>
    /// VisionView.xaml 的交互逻辑
    /// </summary>
    public partial class VisionView : UserControl
    {
        DisplayService _DisplayService;
        CamManager _CamManager;
        VisionViewModel _VisionViewModel;
        IRegionManager _regionManager;

        public VisionView(IContainerProvider container)
        {
            InitializeComponent();
            _regionManager= container.Resolve<IRegionManager>();
            _DisplayService = container.Resolve<DisplayService>();
            _VisionViewModel= container.Resolve<VisionViewModel>();
            _CamManager = container.Resolve<CamManager>();

          
        }

 
    }
}
