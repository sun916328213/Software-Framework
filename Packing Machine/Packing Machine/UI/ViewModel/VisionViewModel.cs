using DPLibrary_Halcon_F48.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prism.Ioc;
using CommunityToolkit.Mvvm.ComponentModel;
using HalconDotNet;
using DPLibrary_Halcon_F48.Tools.FindLine;
using DPLibrary_Halcon_F48.Tools.AI;
using Vision.Core.Tools;   // P2：ToolMge 从 DPLibrary_Halcon_F48.Tools 收回 Vision.Core.Tools

namespace Packing_Machine.UI.ViewModel
{
    public class VisionViewModel
    {
        public VisionModel Datas { get; set; } = new VisionModel();
    }

    public class VisionModel : ObservableObject
    {
        #region 定义工具
        HTool_BarCode_AutoRegionZoomFind HTool_BarCode_AutoRegionZoomFind = new HTool_BarCode_AutoRegionZoomFind();
        /// <summary>
        /// 条码读取
        /// </summary>
        public HTool_BarCode_AutoRegionZoomFind BarcodeReader_Auto
        {
            get => HTool_BarCode_AutoRegionZoomFind;
            set => SetProperty(ref HTool_BarCode_AutoRegionZoomFind, value);
        }

        HTool_BarCode HTool_BarCode=new HTool_BarCode();
        public HTool_BarCode BarcodeReader
        {
            get => HTool_BarCode;
            set => SetProperty(ref HTool_BarCode, value);
        }

        HTool_2DCode HTool_2DCode = new HTool_2DCode();
        /// <summary>
        /// 二维码读取
        /// </summary>
        public HTool_2DCode BarcodeReader_2D
        {
            get => HTool_2DCode;
            set => SetProperty(ref HTool_2DCode, value);
        }

        HTool_TemplateMatch iTemplateMatch_Accessory = new HTool_TemplateMatch();
        /// <summary>
        /// 干燥剂匹配
        /// </summary>
        public HTool_TemplateMatch TemplateMatch_Accessory
        {
            get => iTemplateMatch_Accessory;
            set => SetProperty(ref iTemplateMatch_Accessory, value);
        }

        HTool_TemplateMatch iTemplateMatch_HumidityCardA = new HTool_TemplateMatch();
        /// <summary>
        /// 湿度卡匹配
        /// </summary>
        public HTool_TemplateMatch TemplateMatch_HumidityCardA
        {
            get => iTemplateMatch_HumidityCardA;
            set => SetProperty(ref iTemplateMatch_HumidityCardA, value);
        }

        HTool_TemplateMatch iTemplateMatch_HumidityCardB = new HTool_TemplateMatch();
        /// <summary>
        /// 湿度卡匹配
        /// </summary>
        public HTool_TemplateMatch TemplateMatch_HumidityCardB
        {
            get => iTemplateMatch_HumidityCardB;
            set => SetProperty(ref iTemplateMatch_HumidityCardB, value);
        }

        HTool_TemplateMatch iTemplateMatch_Caliber = new HTool_TemplateMatch();
        /// <summary>
        /// 九点标定模版匹配
        /// </summary>
        public HTool_TemplateMatch TemplateMatch_Caliber
        {
            get => iTemplateMatch_Caliber;
            set => SetProperty(ref iTemplateMatch_Caliber, value);
        }

        HTool_NPoints iHTool_NPoints = new HTool_NPoints();
        /// <summary>
        /// 九点标定
        /// </summary>
        public HTool_NPoints HTool_NPoints
        {
            get => iHTool_NPoints;
            set => SetProperty(ref iHTool_NPoints, value);
        }

        HTool_TransforPoint iHTool_TransforPoint = new HTool_TransforPoint();
        /// <summary>
        /// 仿射变换
        /// </summary>
        public HTool_TransforPoint HTool_TransforPoint
        {
            get => iHTool_TransforPoint;
            set => SetProperty(ref iHTool_TransforPoint, value);
        }

        HTool_FindGroup iBagPattern_Left = new HTool_FindGroup();
        /// <summary>
        /// 找直线 左侧
        /// </summary>
        public HTool_FindGroup BagPattern_Left
        {
            get => iBagPattern_Left;
            set => SetProperty(ref iBagPattern_Left, value);
        }

        HTool_FindGroup iBagPattern_Right = new HTool_FindGroup();
        /// <summary>
        /// 找直线 右侧
        /// </summary>
        public HTool_FindGroup BagPattern_Right
        {
            get => iBagPattern_Right;
            set => SetProperty(ref iBagPattern_Right, value);
        }

        AITool aITool=new AITool();
        public AITool AITool
        {
            get => aITool;
            set => SetProperty(ref aITool, value);
        }

        public void OnProperty(string value)
        {
            OnPropertyChanged(value);
        }

        #endregion


        public VisionModel()
        {
            ToolMge.Instance.Config();
            BarcodeReader_Auto = (HTool_BarCode_AutoRegionZoomFind)ToolMge.Instance[0];
            BarcodeReader_2D = (HTool_2DCode)ToolMge.Instance[1];
            TemplateMatch_HumidityCardA = (HTool_TemplateMatch)ToolMge.Instance[2];
            TemplateMatch_HumidityCardB = (HTool_TemplateMatch)ToolMge.Instance[3];


            TemplateMatch_Accessory = (HTool_TemplateMatch)ToolMge.Instance[4];
            TemplateMatch_Caliber = (HTool_TemplateMatch)ToolMge.Instance[5];
            HTool_NPoints = (HTool_NPoints)ToolMge.Instance[6];
            HTool_TransforPoint = (HTool_TransforPoint)ToolMge.Instance[7];
            BagPattern_Left = (HTool_FindGroup)ToolMge.Instance[8];
            BagPattern_Right = (HTool_FindGroup)ToolMge.Instance[9];

            BarcodeReader = (HTool_BarCode)ToolMge.Instance[10];
            ToolMge.Instance.Add(aITool);
            AITool= (AITool)ToolMge.Instance[15];
        }
    }
}
