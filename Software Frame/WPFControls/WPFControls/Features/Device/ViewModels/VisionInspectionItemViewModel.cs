// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：VisionInspectionItemViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、Device.Controls.Services、Extension.Core.Enums、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Device.Controls.Services;
using Extension.Core.Enums;
using Prism.Ioc;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels
{
    /// <summary>
    /// VisionInspectionItemViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject、INavigationAware。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class VisionInspectionItemViewModel : ObservableObject, INavigationAware
    {
        VisionInspectionItemService visionInspectionItemService;
        public VisionInspectionItemModel Datas => visionInspectionItemService.Model;
        public Array Cams => cameraWorkspace.Model.DataCollection.Select(x => x.Name).ToArray();
        public Array TriggerModes => new[] { "OFF", "Software", "Frame_Line1", "Frame_Line2" };



        CameraWorkspace cameraWorkspace;


        public RelayCommand<VisionInspectionItem> AddLinght { get;}
        public RelayCommand<VisionInspectionItem> ClearLinght { get; }

        /// <summary>
        /// 初始化 VisionInspectionItemViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve、Add、Clear；具体条件和先后次序见方法体。
        /// </summary>
        public VisionInspectionItemViewModel(IContainerProvider container)
        {
            visionInspectionItemService = container.Resolve<VisionInspectionItemService>();
            cameraWorkspace = container.Resolve<CameraWorkspace>();

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

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnNavigatedTo。
        /// </summary>
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 IsNavigationTarget。
        /// </summary>
        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true; // 启用视图缓存，避免每次导航都创建新实例
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnNavigatedFrom。
        /// </summary>
        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            
        }
    }
}

