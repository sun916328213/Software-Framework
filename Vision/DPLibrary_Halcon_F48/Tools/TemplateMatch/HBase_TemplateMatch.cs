using Vision.Core;
using System;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json.Serialization;

namespace DPLibrary_Halcon_F48.Tools
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HSubPixelType
    {
        [Description("无亚像素精度")]
        none,
        [Description("插值法")]
        interpolation,
        [Description("标准最小二乘法")]
        least_squares,
        [Description("高精度的最小二乘")]
        least_squares_high,
        [Description("最高精度的最小二乘")]
        least_squares_very_high,
    }

    /// <summary>
    /// 用于模板匹配的基类
    /// </summary>
    public abstract class HBase_TemplateMatch : HVisionTool
    {
        public HBase_TemplateMatch() 
        {
            Cmd_ChangeTemplateModel.OnBtClick += OnChangeTemplateModel_Clicked;
        }

        protected double mMinScore = 0.5;
        protected int mNumMatches = 1;
        protected double mMaxOverlap = 0.5;
        protected double mGreediness = 0.9;
        protected HSubPixelType mSubPixel = HSubPixelType.least_squares;
        protected int mDeformation = 0;
        public Guid ModelID { get;set; }

        [ToolPropertyAttribute(DisplayName = "工具名称", Group = "1.基础", Description = "用于标识当前工具的唯一名称，方便在多设备场景下区分")]
        /// <summary>
        /// 工具名称
        /// </summary>
        public override string Name { get; set; } = $"Tool_{ToolMge.Instance.GetToolCount<HBase_TemplateMatch>() + 1}";

        [JsonIgnore]
        [ToolPropertyAttribute(DisplayName = "模板", Group = "1.基础", Description = "用于创建或修改模板信息",
            EditorType = E_PropertyEditorType.Button)]
        public ButtonAction Cmd_ChangeTemplateModel { get; protected set; } = new ButtonAction();

        [ToolPropertyAttribute(Group = "2.匹配参数", DisplayName = "最低匹配分数", Description = "最低匹配分数，越大要求越严格，匹配更准确，但可能漏检。推荐 0.5 ~ 0.7",
               EditorType = E_PropertyEditorType.Double, Max = 1, Min = 0, Step = 0.01)]
        /// <summary>
        /// 最低匹配分数
        /// </summary>
        public double MinScore
        {
            get
            {
                return mMinScore;
            }
            set
            {
                if (mMinScore != value)
                {
                    mMinScore = value;
                    OnPropertyChanged(nameof(MinScore));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.匹配参数", DisplayName = "最大匹配个数", Description = "最大匹配个数，1找最优，0返回所有，n返回前n个",
                EditorType = E_PropertyEditorType.Int,  Min = 0 )]
        /// <summary>
        /// 最大匹配个数
        /// </summary>
        public int NumMatches
        {
            get
            {
                return mNumMatches;
            }
            set
            {
                if (mNumMatches != value)
                {
                    mNumMatches = value;
                    OnPropertyChanged(nameof(NumMatches));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.匹配参数", DisplayName = "重叠度", Description = "最大允许的匹配区域重叠度（0~1),0禁止重叠，1允许完全重叠",
                EditorType = E_PropertyEditorType.Double, Max = 1, Min = 0, Step = 0.01)]
        /// <summary>
        /// 重叠度
        /// </summary>
        public double MaxOverlap
        {
            get
            {
                return mMaxOverlap;
            }
            set
            {
                if (mMaxOverlap != value)
                {
                    mMaxOverlap = value;
                    OnPropertyChanged(nameof(MaxOverlap));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.匹配参数", DisplayName = "细分模式", Description = "匹配结果坐标、角度、缩放的精度(NCC无效)",
               EditorType = E_PropertyEditorType.SingleSelect)]
        public HSubPixelType SubPixel
        {
            get
            {
                return mSubPixel;
            }
            set
            {
                if (mSubPixel != value)
                {
                    mSubPixel = value;
                    OnPropertyChanged(nameof(SubPixel));
                }
            }
        }


        [ToolPropertyAttribute(Group = "2.匹配参数", DisplayName = "允许变形度", Description = "0相当于不允许形变，值越大,容错更强，速度下降更容易误检",
                EditorType = E_PropertyEditorType.Int, Max = 32, Min = 0)]
        public int Deformation
        {
            get
            {
                return mDeformation;
            }
            set
            {
                if (mDeformation != value)
                {
                    mDeformation = value;
                    OnPropertyChanged(nameof(Deformation));
                }
            }
        }


        [ToolPropertyAttribute(Group = "2.匹配参数", DisplayName = "贪婪度", Description = "搜索贪婪度（0~1)，基于灰度不支持",
                EditorType = E_PropertyEditorType.Double, Max = 1, Min = 0, Step = 0.01)]
        /// <summary>
        /// 贪婪度
        /// </summary>
        public double Greediness
        {
            get
            {
                return mGreediness;
            }
            set
            {
                if (mGreediness != value)
                {
                    mGreediness = value;
                    OnPropertyChanged(nameof(Greediness));
                }
            }
        }


        /// <summary>
        /// 用于显示工具界面的按钮事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected abstract void OnChangeTemplateModel_Clicked(object sender, EventArgs e);
    }
}
