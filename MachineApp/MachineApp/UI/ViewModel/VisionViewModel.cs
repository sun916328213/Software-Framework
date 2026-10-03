// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：VisionViewModel、VisionModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tools.Halcon.Sessions、Vision.Controls.ViewModels、System、System.Collections.Generic、System.Linq、System.Text。导入不代表所有类型都会在运行时加载。
using Vision.Tools.Halcon.Sessions;
using Vision.Controls.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prism.Ioc;
using CommunityToolkit.Mvvm.ComponentModel;
using HalconDotNet;
using Vision.Core.Tools;   // P2：ToolMge 从 DPLibrary_Halcon_F48.Tools 收回 Vision.Core.Tools

namespace MachineApp.UI.ViewModel
{
    /// <summary>
    /// VisionViewModel 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public class VisionViewModel
    {
        public VisionModel Datas { get; set; } = new VisionModel();
    }

    /// <summary>
    /// VisionModel 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class VisionModel : ObservableObject
    {
        #region 定义工具
        AutoBarcodeSession AutoBarcodeSession = new AutoBarcodeSession();
        /// <summary>
        /// 条码读取
        /// </summary>
        public AutoBarcodeSession BarcodeReader_Auto
        {
            get => AutoBarcodeSession;
            set => SetProperty(ref AutoBarcodeSession, value);
        }

        BarcodeSession BarcodeSession=new BarcodeSession();
        public BarcodeSession BarcodeReader
        {
            get => BarcodeSession;
            set => SetProperty(ref BarcodeSession, value);
        }

        DataCodeSession DataCodeSession = new DataCodeSession();
        /// <summary>
        /// 二维码读取
        /// </summary>
        public DataCodeSession BarcodeReader_2D
        {
            get => DataCodeSession;
            set => SetProperty(ref DataCodeSession, value);
        }

        ShapeMatchSession iTemplateMatch_Accessory = new ShapeMatchSession();
        /// <summary>
        /// 干燥剂匹配
        /// </summary>
        public ShapeMatchSession TemplateMatch_Accessory
        {
            get => iTemplateMatch_Accessory;
            set => SetProperty(ref iTemplateMatch_Accessory, value);
        }

        ShapeMatchSession iTemplateMatch_HumidityCardA = new ShapeMatchSession();
        /// <summary>
        /// 湿度卡匹配
        /// </summary>
        public ShapeMatchSession TemplateMatch_HumidityCardA
        {
            get => iTemplateMatch_HumidityCardA;
            set => SetProperty(ref iTemplateMatch_HumidityCardA, value);
        }

        ShapeMatchSession iTemplateMatch_HumidityCardB = new ShapeMatchSession();
        /// <summary>
        /// 湿度卡匹配
        /// </summary>
        public ShapeMatchSession TemplateMatch_HumidityCardB
        {
            get => iTemplateMatch_HumidityCardB;
            set => SetProperty(ref iTemplateMatch_HumidityCardB, value);
        }

        ShapeMatchSession iTemplateMatch_Caliber = new ShapeMatchSession();
        /// <summary>
        /// 九点标定模版匹配
        /// </summary>
        public ShapeMatchSession TemplateMatch_Caliber
        {
            get => iTemplateMatch_Caliber;
            set => SetProperty(ref iTemplateMatch_Caliber, value);
        }

        CalibrationSession iHTool_NPoints = new CalibrationSession();
        /// <summary>
        /// 九点标定
        /// </summary>
        public CalibrationSession CalibrationSession
        {
            get => iHTool_NPoints;
            set => SetProperty(ref iHTool_NPoints, value);
        }

        TransformPointSession iHTool_TransforPoint = new TransformPointSession();
        /// <summary>
        /// 仿射变换
        /// </summary>
        public TransformPointSession TransformPointSession
        {
            get => iHTool_TransforPoint;
            set => SetProperty(ref iHTool_TransforPoint, value);
        }

        LineGroupSession iBagPattern_Left = new LineGroupSession();
        /// <summary>
        /// 找直线 左侧
        /// </summary>
        public LineGroupSession BagPattern_Left
        {
            get => iBagPattern_Left;
            set => SetProperty(ref iBagPattern_Left, value);
        }

        LineGroupSession iBagPattern_Right = new LineGroupSession();
        /// <summary>
        /// 找直线 右侧
        /// </summary>
        public LineGroupSession BagPattern_Right
        {
            get => iBagPattern_Right;
            set => SetProperty(ref iBagPattern_Right, value);
        }

        DetectionToolSession aITool=new DetectionToolSession();
        public DetectionToolSession DetectionToolSession
        {
            get => aITool;
            set => SetProperty(ref aITool, value);
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnProperty。
        /// 正文中的关键协作调用包括 OnPropertyChanged；具体条件和先后次序见方法体。
        /// </summary>
        public void OnProperty(string value)
        {
            OnPropertyChanged(value);
        }

        #endregion



        /// <summary>
        /// 执行加载操作；对应入口为 Load。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        private static T Load<T>(string name) where T : Vision.Core.Tools.Tool, new()
        {
            if (ToolMge.Instance[name] is T saved) return saved;
            var tool = new T { Name = name };
            ToolMge.Instance.Add(tool);
            return tool;
        }

        /// <summary>
        /// 初始化 VisionModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Config、Load；具体条件和先后次序见方法体。
        /// </summary>
        public VisionModel()
        {
            ToolMge.Instance.Config();
            BarcodeReader_Auto = Load<AutoBarcodeSession>("BarcodeReader_Auto");
            BarcodeReader_2D = Load<DataCodeSession>("BarcodeReader_2D");
            TemplateMatch_HumidityCardA = Load<ShapeMatchSession>("TemplateMatch_HumidityCardA");
            TemplateMatch_HumidityCardB = Load<ShapeMatchSession>("TemplateMatch_HumidityCardB");
            TemplateMatch_Accessory = Load<ShapeMatchSession>("TemplateMatch_Accessory");
            TemplateMatch_Caliber = Load<ShapeMatchSession>("TemplateMatch_Caliber");
            CalibrationSession = Load<CalibrationSession>("CalibrationSession");
            TransformPointSession = Load<TransformPointSession>("TransformPointSession");
            BagPattern_Left = Load<LineGroupSession>("BagPattern_Left");
            BagPattern_Right = Load<LineGroupSession>("BagPattern_Right");
            BarcodeReader = Load<BarcodeSession>("BarcodeReader");
            DetectionToolSession = Load<DetectionToolSession>("DetectionToolSession");
        }
    }
}
