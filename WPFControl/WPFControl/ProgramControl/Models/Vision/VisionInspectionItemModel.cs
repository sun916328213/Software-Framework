using CommunityToolkit.Mvvm.ComponentModel;
using Device.Camera.Gige.Definition;
using Device.Light.Definition;
using Extension.UI.Controls;
using Mapster;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControl.CustomControl.Models;
using WPFCustomControls.Controls;

namespace WPFControl.ProgramControl.Models
{
    /// <summary>
    /// 视觉检测项目
    /// </summary>
    public class VisionInspectionItem : ObservableObject, IGigeCamControlParam
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

        ETriggerMode triggerMode;
        /// <summary>
        /// 触发模式
        /// </summary>
        public ETriggerMode TriggerMode
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

    public static class VisionInspectionItemModelMessage
    {
        public static readonly string[] 视觉检测项设置 = { "视觉检测项设置", "Vision Inspection Items Settings" };

    }

    /// <summary>
    /// 视觉检测项目
    /// </summary>
    public class VisionInspectionItemModel : ConfigManagerModel<VisionInspectionItem>
    {
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
