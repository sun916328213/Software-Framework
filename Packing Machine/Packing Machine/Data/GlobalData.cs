using DataManager.Log.Definition;
using Device.Light.Definition;
using Device.Scan.Definition;
using Extension.Core.Conversion;
using System.Collections.ObjectModel;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;
using static WPFControl.ProgramControl.Models.VisionInspectionItemModel;

namespace Packing_Machine.Data
{
    public static class GlobalData
    {
        const string FilePath = "Config\\GlobalData.json";
        /// <summary>
        /// 存档路径
        /// </summary>
        public static string Path_Image { get; set; } = "D:\\Image";
        /// <summary>
        /// 保存图像天数
        /// </summary>
        public static int SaveImageDay { get; set; } = 100;
 
        /// <summary>
        /// PLC 地址存储路径
        /// </summary>
        public static string PLCPath { get; set; } = "Config//PLCIO.csv";
        /// <summary>
        /// 湿度卡数量
        /// </summary>
        public static int Num_HumidityIndicator { get; set; } = 1;
        /// <summary>
        /// 干燥剂数量
        /// </summary>
        public static int Num_Desiccant { get; set; } = 4;

        /// <summary>
        /// FOSB贴后标签检测
        /// </summary>
        public static VisionInspectionItem VisionItem_Label_FOSBID { get; set; } = new VisionInspectionItem()
        {
            Name = "",
            Exposure = 200,
            LightItems = new ObservableCollection<LightItem>() { new LightItem { ChannelIndex = 1, LightIntensity = 255 } }
        };

        /// <summary>
        /// FOSB贴后标签检测
        /// </summary>
        public static VisionInspectionItem VisionItem_Label_FOSB { get; set; } = new VisionInspectionItem()
        {
            Name = "",
            Exposure = 1000,
            Gain = 10,
            LightItems = new ObservableCollection<LightItem>()
        };

        /// <summary>
        /// AL贴后标签检测
        /// </summary>
        public static VisionInspectionItem VisionItem_Label_AL { get; set; } = new VisionInspectionItem()
        {
            Name = "0",
            Exposure = 1000,
            Gain = 10,
            LightItems = new ObservableCollection<LightItem>()
        };

        ///光源ID 进料1 机械手2 打印机3 配件4 热封5 6  AL1=8 AL2=7

        /// <summary>
        /// 贴前标签检测
        /// </summary>
        public static VisionInspectionItem VisionItem_LabelPrint { get; set; } = new VisionInspectionItem()
        {
            Name = "0",
            Exposure = 300,
            LightItems = new ObservableCollection<LightItem>() { new LightItem { ChannelIndex = 3, LightIntensity = 255 } }
        };

        /// <summary>
        /// 适度卡检测
        /// </summary>
        public static VisionInspectionItem VisionItem_HI { get; set; } = new VisionInspectionItem()
        {
            Name = "0",
            Exposure = 20,
            LightItems = new ObservableCollection<LightItem>() { new LightItem { ChannelIndex = 2, LightIntensity = 255 } }
        };

        /// <summary>
        /// 存档
        /// </summary>
        public static VisionInspectionItem VisionItem_Arch { get; set; } = new VisionInspectionItem()
        {
            Name = "1",
            Exposure = 200,
            LightItems = new ObservableCollection<LightItem>() { new LightItem { ChannelIndex = 1, LightIntensity = 255 } }
        };

        /// <summary>
        /// 铝塑左侧检测
        /// </summary>
        public static VisionInspectionItem VisionItem_ALLeft { get; set; } = new VisionInspectionItem()
        {
            Name = "3",
            Exposure = 60,
            LightItems = new ObservableCollection<LightItem>() { new LightItem { ChannelIndex = 8, LightIntensity = 255 } }
        };

        /// <summary>
        /// 铝塑袋右侧检测
        /// </summary>
        public static VisionInspectionItem VisionItem_ALRight { get; set; } = new VisionInspectionItem()
        {
            Name = "2",
            Exposure = 60,
            LightItems = new ObservableCollection<LightItem>() { new LightItem { ChannelIndex = 7, LightIntensity = 255 } }
        };
        
        /// <summary>
        /// 配件检测
        /// </summary>
        public static VisionInspectionItem VisionItem_Accessory { get; set; } = new VisionInspectionItem()
        {
            Name = "4",
            Exposure = 60,
            LightItems = new ObservableCollection<LightItem>() { new LightItem { ChannelIndex = 4, LightIntensity = 255 } }
        };

        /// <summary>
        /// 袋口检测
        /// </summary>
        public static VisionInspectionItem VisionItem_AI { get; set; } = new VisionInspectionItem()
        {
            Name = "5",
            Exposure = 10,
            LightItems = new ObservableCollection<LightItem>()
            {
                new LightItem { ChannelIndex = 5, LightIntensity = 255 } ,
                new LightItem{ ChannelIndex=6, LightIntensity=255 }
            }
        };

        public static LightParam LightParam { get; set; } = new LightParam
        {
            Name = "光源控制器",
            ComName = "COM9",
            LightType = ELightType.CST,
        };

        public static ScanParam ScanPara { get; set; } = new ScanParam
        {
            Name = "扫码枪",
            PortName = "COM1",
            ScanType = EScanType.新大陆,
        };

        /// <summary>
        /// 保存数据到JSON文件
        /// </summary>
        public static void Save()
        {
            JsonEx.SaveStaticClass(typeof(GlobalData), FilePath);
        }

        /// <summary>
        /// 从JSON文件加载数据
        /// </summary>
        public static void Load()
        {
            JsonEx.LoadStaticClass(typeof(GlobalData), FilePath);
        }
    }
}