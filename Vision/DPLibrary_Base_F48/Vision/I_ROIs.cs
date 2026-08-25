using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows;

namespace DPLibrary_Base_F48.Vision
{
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

    public interface I_IsHit 
    {
        /// <summary>
        /// 是否选中
        /// </summary>
        bool IsHit { get; set; }
    }

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
        Point LUPoint { get; }
        Point RDPoint { get; }
        Point RUPoint { get; }
        Point LDPoint { get; }

    }

    /// <summary>
    /// 带旋转矩形的区域
    /// </summary>
    public interface I_ROI_Rectangle2Data
    {
        Point CenterPoint { get;  }
        double Angle { get;  }
        double Len1 { get;  }
        double Len2 { get; }
    }


    /// <summary>
    /// 多边形的区域
    /// </summary>
    public interface I_ROI_PolylineData
    {
        List<Point> Points { get; }
        int PointsCount { get; }
    }
}
