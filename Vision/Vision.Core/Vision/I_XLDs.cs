using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows;

namespace Vision.Core.Vision
{
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
        Point StartPoint { get; }
        Point EndPoint { get; }
    }

    /// <summary>
    /// 圆弧的xld数据
    /// </summary>
    public interface I_XLD_SectorCircleData : INotifyPropertyChanged
    {
        /// <summary>
        /// 圆心
        /// </summary>
        Point CenterPoint { get; }
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
