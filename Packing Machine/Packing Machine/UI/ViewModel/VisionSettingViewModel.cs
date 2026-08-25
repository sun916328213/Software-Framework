using CommunityToolkit.Mvvm.Input;
using DPLibrary_Halcon_F48.Tools;
using Packing_Machine.Infra.Tool;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControl.ProgramControl.Services;

namespace Packing_Machine.UI.ViewModel
{
    public class VisionSettingViewModel
    {
        VisionInspectionItemService visionInspectionItemService;
        VisionTool visionTool;
        VisionViewModel  visionViewModel;
        public IRelayCommand RunCommand { get; set;}
        public IRelayCommand ShowToolCommand { get; set; }

        public VisionSettingViewModel(IContainerProvider container)
        {
            visionInspectionItemService = container.Resolve<VisionInspectionItemService>();
            visionTool= container.Resolve<VisionTool>();

            RunCommand = new RelayCommand(() =>
            {
                var curitem = visionInspectionItemService.Model.CurVisionInspectionItem;
                string camname = curitem.CamName;
                string toolname = curitem.Name;
                var image=visionTool.GrabImage(null, null, curitem, false);
                var tool = (HVisionTool)ToolMge.Instance[toolname];
                tool.Image = image;
                tool.Run();
            });

            ShowToolCommand = new RelayCommand(() =>
            {
                var curitem = visionInspectionItemService.Model.CurVisionInspectionItem;
                string camname = curitem.CamName;
                string toolname = curitem.Name;
                
            });
        }
    }
}
