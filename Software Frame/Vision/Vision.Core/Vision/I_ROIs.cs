// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：E_ROIType、I_IsHit、I_ROI、I_ROI_RectangleData、I_ROI_Rectangle2Data、I_ROI_PolylineData。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core、System、System.Collections.Generic、System.ComponentModel、System.Text.Json.Serialization、Vision.Core.Geometry。导入不代表所有类型都会在运行时加载。
using Vision.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Vision.Core.Geometry;

namespace Vision.Core.Vision
{
    /// <summary>
    /// E_ROIType 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum E_ROIType
    {
        Null,
        Rectangle,
        Rectangle2,
        Circle,
        Ellipsoid,
        Polygon
    }

    /// <summary>
    /// I_IsHit 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public interface I_IsHit 
    {
        /// <summary>
        /// 是否选中
        /// </summary>
        bool IsHit { get; set; }
    }

    /// <summary>
    /// I_ROI 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：I_IsHit。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public interface I_ROI:I_IsHit
    {

        E_VisionType VisionType { get; }

        E_ROIType ROIType { get; }

        string ROIName { get; set; }

        /// <summary>
        /// 是否填充
        /// </summary>
        bool IsFull { get; set; }
        /// <summary>
        /// 显示颜色
        /// </summary>
        E_VisionColor DispColor { get; set; }
        /// <summary>
        /// 选中时展示的颜色
        /// </summary>
        E_VisionColor DispSelectColor { get; set; }

    }

    /// <summary>
    /// 矩形区域的附带数据
    /// </summary>
    public interface I_ROI_RectangleData : INotifyPropertyChanged
    {
        Point2D LUPoint { get; }
        Point2D RDPoint { get; }
        Point2D RUPoint { get; }
        Point2D LDPoint { get; }

    }

    /// <summary>
    /// 带旋转矩形的区域
    /// </summary>
    public interface I_ROI_Rectangle2Data
    {
        Point2D CenterPoint { get;  }
        double Angle { get;  }
        double Len1 { get;  }
        double Len2 { get; }
    }


    /// <summary>
    /// 多边形的区域
    /// </summary>
    public interface I_ROI_PolylineData
    {
        List<Point2D> Points { get; }
        int PointsCount { get; }
    }
}
