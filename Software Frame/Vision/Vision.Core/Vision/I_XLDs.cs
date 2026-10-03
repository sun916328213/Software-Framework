// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：E_XLDType、I_XLD、I_XLD_LineData、I_XLD_SectorCircleData。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core、System.ComponentModel、System.Text.Json.Serialization、Vision.Core.Geometry。导入不代表所有类型都会在运行时加载。
using Vision.Core;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Vision.Core.Geometry;

namespace Vision.Core.Vision
{
    /// <summary>
    /// E_XLDType 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum E_XLDType
    {
        Null,
        Line,
        SectorCircle,
        SectorEllipsoid
    }

    /// <summary>
    /// 轮廓接口
    /// </summary>
    public interface I_XLD: I_IsHit
    {
        E_VisionType VisionType { get; }

        E_XLDType XLDType { get; }

        string XLDName { get; set; }

        /// <summary>
        /// 显示颜色
        /// </summary>
        E_VisionColor DispColor { get; set; }
        /// <summary>
        /// 选中时展示的颜色
        /// </summary>
        E_VisionColor DispSelectColor { get; set; }
        /// <summary>
        /// 显示线宽
        /// </summary>
        double LineSize { get; set; }
    }

    /// <summary>
    /// 线段的xld数据
    /// </summary>
    public interface I_XLD_LineData : INotifyPropertyChanged
    {
        Point2D StartPoint { get; }
        Point2D EndPoint { get; }
    }

    /// <summary>
    /// 圆弧的xld数据
    /// </summary>
    public interface I_XLD_SectorCircleData : INotifyPropertyChanged
    {
        /// <summary>
        /// 圆心
        /// </summary>
        Point2D CenterPoint { get; }
        /// <summary>
        /// 半径
        /// </summary>
        double Radius { get; }
        /// <summary>
        /// 起始角度
        /// </summary>
        double StartAngle { get; }
        /// <summary>
        /// 结束角度
        /// </summary>
        double EndAngle { get; }
    }
}
