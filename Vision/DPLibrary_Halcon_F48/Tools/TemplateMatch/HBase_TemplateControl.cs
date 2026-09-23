
using DPLibrary_Base_F48;
using DPLibrary_Base_F48.Tools;
using HalconDotNet;
using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TemplateType 
    {
        [Description("基于形状的匹配")]
        ShapeBasedMatching,
        [Description("基于灰度的匹配")]
        GrayBasedMatching
    }


    /// <summary>
    /// 点数优化类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HOptimizationType
    {
       [Description("不变")]
        none,
        [Description("少量减少点，速度有提升，精度基本保持")]
        point_reduction_low,
        [Description("中等程度减少点，速度提升明显，精度略降")]
        point_reduction_medium,
        [Description("大量减少点，速度最快，但可能影响匹配精度")]
        point_reduction_high,
        [Description("自动选择")]
        auto    
    }

   /// <summary>
   /// 匹配方式
   /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HMetricType
    {
        [Description("要求模板与待匹配目标的亮暗关系一致")]
        use_polarity,
        [Description("亮暗反转")]
        ignore_global_polarity,
        [Description("局部对比度也可以反转")]
        ignore_local_polarity,
        [Description("彩色图像（多通道），允许局部颜色通道的极性变化")]
        ignore_color_polarity
    }
    /// <summary>
    /// 用于形状模板创建的工具
    /// </summary>
    public abstract class HBase_TemplateControl : HVisionTool
    {
        public HBase_TemplateControl() 
        {
            Cmd_GetTemplateImage.OnBtClick += OnGetTemplateImage;
        }

        /// <summary>
        /// 模板设置界面 获取工具输入图像事件
        /// </summary>
        /// <param name="arg1"></param>
        /// <param name="args"></param>
        public abstract void OnGetTemplateImage(object arg1, EventArgs args);
 
        protected int mLevels = -1;

        protected double mAngleStart = 0;

        protected double mAngleExtent = 0;

        protected int mAngleStep = -1;

        protected double mScaleMin = 0.9;

        protected double mScaleMax = 1.1;

        protected double mScaleStep = 0.01;

        protected int mContrast = -1;

        protected int mMinContrast = -1;

        protected HOptimizationType mOptimization = HOptimizationType.none;

        protected HMetricType mHMetric = HMetricType.use_polarity;

        Point mBasePoint = new Point();

        [ToolPropertyAttribute(DisplayName = "工具名称", Group = "1.基础", Description = "用于标识当前工具的唯一名称，方便在多设备场景下区分")]
        /// <summary>
        /// 工具名称
        /// </summary>
        public override string Name { get; set; } = $"Tool_{ToolMge.Instance.GetToolCount<HBase_TemplateControl>() + 1}";


        [JsonIgnore]
        [ToolPropertyAttribute(DisplayName = "获取图像", Group = "1.基础", Description = "从工具中获取输入图像",
           EditorType = E_PropertyEditorType.Button)]
        public ButtonAction Cmd_GetTemplateImage { get; protected set; } = new ButtonAction();



        [ToolPropertyAttribute(Group = "2.训练参数", DisplayName = "模型类型", Description = "生成的模型的类型",
                       EditorType = E_PropertyEditorType.SingleSelect)]
        public abstract TemplateType TemplateModelType { get; set; }

        [ToolPropertyAttribute(Group = "2.训练参数", DisplayName = "金字塔层数", Description = "金字塔层数，-1就是自动",
                               EditorType = E_PropertyEditorType.Double, Max = 10, Min = -1, Step = 1)]
        /// <summary>
        /// 金字塔层数
        /// </summary>
        public int Levels
        {
            get
            {
                return mLevels;
            }
            set
            {
                if (mLevels != value)
                {
                    mLevels = value;
                    OnPropertyChanged(nameof(Levels));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.训练参数", DisplayName = "起始角度", Description = "起始角度",
                               EditorType = E_PropertyEditorType.Double, Max = 180, Min = -180, Step = 0.1)]
        /// <summary>
        /// 起始角度
        /// </summary>
        public double AngleStart
        {
            get
            {
                return mAngleStart;
            }
            set
            {
                if (mAngleStart != value)
                {
                    mAngleStart = value;
                    OnPropertyChanged(nameof(AngleStart));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.训练参数", DisplayName = "角度范围", Description = "角度范围必须 ≥ 0",
                       EditorType = E_PropertyEditorType.Double, Max = 360, Min = 0, Step = 0.1)]
        /// <summary>
        /// 角度范围
        /// </summary>
        public double AngleExtent
        {
            get
            {
                return mAngleExtent;
            }
            set
            {
                if (mAngleExtent != value)
                {
                    mAngleExtent = value;
                    OnPropertyChanged(nameof(AngleExtent));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.训练参数", DisplayName = "角度步长", Description = "角度步长，决定角度分辨率,-1为自动识别",
               EditorType = E_PropertyEditorType.Int, Max = 180, Min = -1)]
        /// <summary>
        /// 角度步长
        /// </summary>
        public int AngleStep
        {
            get
            {
                return mAngleStep;
            }
            set
            {
                if (mAngleStep != value)
                {
                    mAngleStep = value;
                    OnPropertyChanged(nameof(AngleStep));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.训练参数", DisplayName = "最小缩放", Description = "最小缩放,基于灰度不支持缩放",
                       EditorType = E_PropertyEditorType.Double, Max = 1, Min = 0, Step = 0.001)]
        /// <summary>
        /// 最小缩放
        /// </summary>
        public double ScaleMin
        {
            get
            {
                return mScaleMin;
            }
            set
            {
                if (mScaleMin != value)
                {
                    mScaleMin = value;
                    OnPropertyChanged(nameof(ScaleMin));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.训练参数", DisplayName = "最大缩放", Description = "最大缩放,基于灰度不支持缩放",
                       EditorType = E_PropertyEditorType.Double, Max = 10, Min = 1, Step = 0.01)]
        /// <summary>
        /// 最大缩放
        /// </summary>
        public double ScaleMax
        {
            get
            {
                return mScaleMax;
            }
            set
            {
                if (mScaleMax != value)
                {
                    mScaleMax = value;
                    OnPropertyChanged(nameof(ScaleMax));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.训练参数", DisplayName = "缩放步长", Description = "缩放步长,基于灰度不支持缩放",
               EditorType = E_PropertyEditorType.Double, Min = 0,Step = 0.01)]
        /// <summary>
        /// 缩放步长
        /// </summary>
        public double ScaleStep
        {
            get
            {
                return mScaleStep;
            }
            set
            {
                if (mScaleStep != value)
                {
                    mScaleStep = value;
                    OnPropertyChanged(nameof(ScaleStep));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.训练参数", DisplayName = "模型点数优化", Description = "影响模型的大小、匹配速度和内存占用",
               EditorType = E_PropertyEditorType.SingleSelect)]
        public HOptimizationType Optimization
        {
            get
            {
                return mOptimization;
            }
            set
            {
                if (mOptimization != value)
                {
                    mOptimization = value;
                    OnPropertyChanged(nameof(Optimization));
                }
            }
        }
        
        [ToolPropertyAttribute(Group = "2.训练参数", DisplayName = "极性方式", Description = "控制模板和目标图像在 亮暗关系（极性） 上是否必须一致",
               EditorType = E_PropertyEditorType.SingleSelect)]
        public HMetricType HMetric
        {
            get
            {
                return mHMetric;
            }
            set
            {
                if (mHMetric != value)
                {
                    mHMetric = value;
                    OnPropertyChanged(nameof(HMetric));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.训练参数", DisplayName = "提取时对比度", Description = "控制在建模板时，哪些边缘点/特征点被采纳进入模板",
               EditorType = E_PropertyEditorType.Int, Max = 255, Min = -1)]
        public int Contrast
        {
            get
            {
                return mContrast;
            }
            set
            {
                if (mContrast != value)
                {
                    mContrast = value;
                    OnPropertyChanged(nameof(Contrast));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.训练参数", DisplayName = "匹配对比度", Description = "控制运行时 匹配能接受的最小灰度差",
                EditorType = E_PropertyEditorType.Int, Max = 255, Min = -1)]
        public int MinContrast
        {
            get
            {
                return mMinContrast;
            }
            set
            {
                if (mMinContrast != value)
                {
                    mMinContrast = value;
                    OnPropertyChanged(nameof(MinContrast));
                }
            }
        }

        // ⚠️ setter 是 protected，System.Text.Json 默认**不给非公有 setter 赋值且静默跳过**
        // ⇒ 读档后训练中心点会静默回退成 Point(0,0)（JSON 里明明写着 "BasePoint"）。
        // 加 [JsonInclude] 让 STJ 使用这个非公有 setter；保留 protected 是因为它是训练结果，
        // 面板里应保持只读（改 public set 会让它在属性面板里变成可手改）。
        [JsonInclude]
        [JsonPropertyName("BasePoint")]
        [ToolPropertyAttribute(Group = "3.训练结果", DisplayName = "基础中心点", Description = "训练后的中心点，作为后续匹配的基准",
                EditorType = E_PropertyEditorType.Text)]
        public Point BasePoint 
        { 
            get { return mBasePoint; }
            protected set 
            {
                if (mBasePoint != value)
                {
                    mBasePoint = value;
                    OnPropertyChanged(nameof(BasePoint));
                }
            }
        }

        [JsonIgnore]
        public HTuple Model;

    }
}
