// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：VisionView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core.Vision、HalconDotNet、MachineApp.UI.ViewModel、Prism.Ioc、Prism.Regions、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using Vision.Core.Vision;
using HalconDotNet;
using MachineApp.UI.ViewModel;
using Prism.Ioc;
using Prism.Regions;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPFControls.ProgramControl.Services;

namespace MachineApp.UI.View
{
    /// <summary>
    /// VisionView.xaml 的交互逻辑
    /// </summary>
    public partial class VisionView : UserControl
    {
        DisplayService _DisplayService;

        VisionViewModel _VisionViewModel;
        IRegionManager _regionManager;

        /// <summary>
        /// 初始化 VisionView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent、Resolve；具体条件和先后次序见方法体。
        /// </summary>
        public VisionView(IContainerProvider container)
        {
            InitializeComponent();
            _regionManager= container.Resolve<IRegionManager>();
            _DisplayService = container.Resolve<DisplayService>();
            _VisionViewModel= container.Resolve<VisionViewModel>();


          
        }

 
    }
}
