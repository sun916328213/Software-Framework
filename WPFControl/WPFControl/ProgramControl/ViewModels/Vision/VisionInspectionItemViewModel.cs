using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Device.Camera.Gige.Definition;
using Device.Light.Definition;
using Extension.Core.Enums;
using Prism.Ioc;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    public class VisionInspectionItemViewModel : ObservableObject, INavigationAware
    {
        VisionInspectionItemService visionInspectionItemService;
        public VisionInspectionItemModel Datas => visionInspectionItemService.Model;
        public Array Cams => camManagerService.Model.DataCollection.Select(x => x.Name).ToArray();
        public Array TriggerModes => (EnumEx.GetName<ETriggerMode>());



        CamManagerService camManagerService;


        public RelayCommand<VisionInspectionItem> AddLinght { get;}
        public RelayCommand<VisionInspectionItem> ClearLinght { get; }

        public VisionInspectionItemViewModel(IContainerProvider container)
        {
            visionInspectionItemService = container.Resolve<VisionInspectionItemService>();
            camManagerService = container.Resolve<CamManagerService>();

            AddLinght = new RelayCommand<VisionInspectionItem>(item =>
            {
                if (item != null)
                {
                    item.LightItems.Add(new LightItem());
                }
            });

            ClearLinght = new RelayCommand<VisionInspectionItem>(item =>
            {
                if (item != null)
                {
                    item.LightItems.Clear();
                }
            });
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true; // 启用视图缓存，避免每次导航都创建新实例
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            
        }
    }
}

