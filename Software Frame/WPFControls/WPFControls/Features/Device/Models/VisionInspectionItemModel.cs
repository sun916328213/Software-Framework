// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：VisionInspectionItem、VisionInspectionItemModelMessage、VisionInspectionItemModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、Extension.UI.Controls、Mapster、System、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using Extension.UI.Controls;
using Mapster;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControls.Controls.Models;
using WPFCustomControls.Controls;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// 视觉检测项目
    /// </summary>
    public class VisionInspectionItem : ObservableObject
    {
        string name,camname,toolname;
        /// <summary>
        /// 项目名称
        /// </summary>
        public   string Name
        {
            get => name;
            set=>SetProperty(ref name, value);
        }
        /// <summary>
        /// 相机名称
        /// </summary>
        public string CamName
        {
            get => camname;
            set => SetProperty(ref camname, value);
        }
        /// <summary>
        /// 工具名称
        /// </summary>
        public string ToolName
        {
            get => toolname;
            set => SetProperty(ref toolname, value);
        }

        int exposure;
        /// <summary>
        /// 曝光时间
        /// </summary>
        public int Exposure
        {
            get => exposure;
            set => SetProperty(ref exposure, value);
        }

        double gain;
        /// <summary>
        /// 亮度
        /// </summary>
        public double Gain
        {
            get => gain;
            set => SetProperty(ref gain, value);
        }  

        string triggerMode = "Software";
        /// <summary>
        /// 触发模式
        /// </summary>
        public string TriggerMode
        {
            get => triggerMode;
            set => SetProperty(ref triggerMode, value);
        }

        ObservableCollection<LightItem> lightItems=new ObservableCollection<LightItem>();
        /// <summary>
        /// 光源亮度参数
        /// </summary>
        public ObservableCollection<LightItem> LightItems
        {
            get => lightItems;
            set => SetProperty(ref lightItems, value);
        }
    }

    /// <summary>
    /// VisionInspectionItemModelMessage 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public static class VisionInspectionItemModelMessage
    {
        public static readonly string[] 视觉检测项设置 = { "视觉检测项设置", "Vision Inspection Items Settings" };

    }

    /// <summary>
    /// 视觉检测项目
    /// </summary>
    public class VisionInspectionItemModel : ConfigManagerModel<VisionInspectionItem>
    {
        /// <summary>
        /// 初始化 VisionInspectionItemModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 GetMessage；具体条件和先后次序见方法体。
        /// </summary>
        public VisionInspectionItemModel()
        {
            Name = LangSwitchEx.GetMessage(VisionInspectionItemModelMessage.视觉检测项设置);
            FilePath = "Config//";
            FileName = "VisionInspectionItem.json";
        }

        VisionInspectionItem curvisioninspectionitem=new VisionInspectionItem();
        /// <summary>
        /// 当前视觉检测项目
        /// </summary>
        public VisionInspectionItem CurVisionInspectionItem
        {
            get => curvisioninspectionitem; 
            set=>SetProperty(ref curvisioninspectionitem, value);
        }
    }
}
